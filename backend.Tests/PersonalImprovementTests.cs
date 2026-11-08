using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

public class PersonalImprovementTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<PersonalImprovementService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<AiContextBuilder>();
        services.AddScoped<AiCoachService>();
        services.AddScoped<OutboxPublisher>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(Options.Create(new KonkoorHelp.Api.Services.Integrations.IntegrationsOptions
        {
            WebhookProtectionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        }));
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookSecretProtector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookDnsResolver, KonkoorHelp.Api.Services.Integrations.SystemWebhookDnsResolver>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector, KonkoorHelp.Api.Services.Integrations.TcpWebhookSocketConnector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookUrlSafety>();
        services.AddScoped<KonkoorHelp.Api.Services.Integrations.WebhookService>();
        services.AddHttpClient(nameof(KonkoorHelp.Api.Services.Integrations.WebhookService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
            KonkoorHelp.Api.Services.Integrations.WebhookSocketsHttpHandlerFactory.Create(
                sp.GetRequiredService<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector>()));
        services.AddSingleton<IOptions<AiOptions>>(Options.Create(new AiOptions { DefaultProvider = "heuristic" }));
        services.AddSingleton<IAiProvider, NullAiProvider>();
        services.AddSingleton<IAiProvider, HeuristicAiProvider>();
        services.AddSingleton<IAiProviderResolver, AiProviderResolver>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "imp" + Guid.NewGuid().ToString("N")[..6],
            NormalizedUsername = Guid.NewGuid().ToString("N"),
            PasswordHash = "x",
            DisplayName = "بهبود",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var workspaceId = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Type = WorkspaceType.Personal,
            Name = "شخصی",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Data = "{}",
            LocaleDefault = "fa-IR",
            TimeZone = "Asia/Tehran",
            FeatureFlags = """{"ai":true,"aiCoach":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        db.AiWorkspaceSettings.Add(new AiWorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Enabled = true,
            ProviderCode = "heuristic",
            Model = "",
            AllowUnderAge = true,
            MinAgeYears = 13,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (sp, workspaceId, user.Id);
    }

    [Fact]
    public async Task Cold_start_collecting_until_min_evidence()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<PersonalImprovementService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Subject = "ریاضی",
            Topic = "جبر",
            ScoreEma = 0.4,
            EvidenceCount = 1,
            LastEvidenceAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var json = JsonSerializer.SerializeToElement(await svc.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("Collecting", json.GetProperty("composite").GetProperty("status").GetString());
        Assert.Contains("جمع‌آوری", json.GetProperty("composite").GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task Baseline_ready_uses_mastery_and_assessment_not_xp()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<PersonalImprovementService>();

        var start = DateTime.UtcNow.AddDays(-10);
        for (var i = 0; i < 5; i++)
        {
            db.MasteryNodes.Add(new MasteryNode
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                UserId = userId,
                Subject = "فیزیک",
                Topic = $"t{i}",
                ScoreEma = 0.35 + i * 0.02,
                EvidenceCount = 2,
                LastEvidenceAt = start.AddDays(i),
                UpdatedAt = start.AddDays(i),
            });
        }

        var assessmentId = Guid.NewGuid();
        db.Assessments.Add(new AssessmentDefinition
        {
            Id = assessmentId,
            WorkspaceId = ws,
            Title = "آزمون",
            Subject = "فیزیک",
            Kind = AssessmentKind.SelfAssessment,
            Status = AssessmentStatus.Published,
            CreatedByUserId = userId,
            CreatedAt = start,
            UpdatedAt = start,
        });
        db.AssessmentAttempts.Add(new AssessmentAttempt
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            AssessmentId = assessmentId,
            UserId = userId,
            Status = AttemptStatus.Scored,
            StartedAt = start.AddDays(1),
            SubmittedAt = start.AddDays(1),
            Percent = 70,
            Score = 70,
            MaxScore = 100,
        });

        // XP alone must not establish baseline — leave XP ledger empty / irrelevant.
        db.XpLedger.Add(new XpLedger
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Delta = 9999,
            Kind = LedgerKind.Earn,
            Reason = "farm",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var first = JsonSerializer.SerializeToElement(await svc.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("Ready", first.GetProperty("composite").GetProperty("status").GetString());
        Assert.True(await db.PersonalBaselines.AnyAsync(b => b.WorkspaceId == ws && b.UserId == userId));

        // Raise current mastery above persisted baseline.
        foreach (var node in db.MasteryNodes.Where(n => n.WorkspaceId == ws))
        {
            node.ScoreEma = 0.9;
            node.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var json = JsonSerializer.SerializeToElement(await svc.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("Ready", json.GetProperty("composite").GetProperty("status").GetString());
        Assert.True(json.GetProperty("composite").GetProperty("improvementPercent").GetDouble() > 0);
    }

    [Fact]
    public async Task Tenant_isolation_on_improvement_and_privacy_default_private()
    {
        var (spA, wsA, userA) = await CreateAsync();
        var (spB, wsB, userB) = await CreateAsync();
        await using var scopeA = spA.CreateAsyncScope();
        await using var scopeB = spB.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var boardsA = scopeA.ServiceProvider.GetRequiredService<LeaderboardService>();
        var svcA = scopeA.ServiceProvider.GetRequiredService<PersonalImprovementService>();

        await SeedReadyEvidenceAsync(dbA, wsA, userA);
        await SeedReadyEvidenceAsync(scopeB.ServiceProvider.GetRequiredService<AppDbContext>(), wsB, userB);

        var me = JsonSerializer.SerializeToElement(await svcA.GetMeAsync(wsA, userA, CancellationToken.None));
        Assert.Equal("Ready", me.GetProperty("composite").GetProperty("status").GetString());
        Assert.Equal(0, await dbA.PersonalBaselines.CountAsync(b => b.WorkspaceId == wsB));

        var privacy = JsonSerializer.SerializeToElement(await boardsA.GetPrivacyAsync(wsA, userA, CancellationToken.None));
        Assert.False(privacy.GetProperty("leaderboardVisible").GetBoolean());
        Assert.Equal("private", privacy.GetProperty("privacyDefault").GetString());

        var board = JsonSerializer.SerializeToElement(await boardsA.GetBoardAsync(
            wsA, LeaderboardMetric.LearningImprovement, "all", "فیزیک", null, 1, 20, CancellationToken.None));
        Assert.Equal("peerComparison", board.GetProperty("viewKind").GetString());
        Assert.True(board.GetProperty("peerComparison").GetBoolean());
        // Privacy row created with LeaderboardVisible=false → not on peer board / total does not leak peers.
        Assert.Equal(0, board.GetProperty("entries").GetArrayLength());
        Assert.Equal(0, board.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Coach_chat_returns_evidenceRefs_and_confidence()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Subject = "شیمی",
            Topic = "پیوند",
            ScoreEma = 0.2,
            EvidenceCount = 3,
            LastEvidenceAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var json = JsonSerializer.SerializeToElement(
            await coach.ChatAsync(ws, userId, "کجا ضعیف‌ترم؟", null, CancellationToken.None));

        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("reply").GetString()));
        Assert.Contains(json.GetProperty("confidence").GetString(), new[] { "high", "medium", "low" });
        Assert.True(json.GetProperty("evidenceRefs").GetArrayLength() >= 1);
        Assert.True(await db.CoachThreads.AnyAsync(t => t.WorkspaceId == ws && t.UserId == userId));
        Assert.Equal(2, await db.CoachMessages.CountAsync(m => m.ThreadId == json.GetProperty("threadId").GetGuid()));
    }

    [Fact]
    public async Task Coach_chat_heuristic_fallback_when_provider_fails()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();
        db.AiWorkspaceSettings.First(s => s.WorkspaceId == ws).ProviderCode = "null";
        await db.SaveChangesAsync();

        var json = JsonSerializer.SerializeToElement(
            await coach.ChatAsync(ws, userId, "راهنمایی بده", null, CancellationToken.None));
        Assert.Equal("heuristic", json.GetProperty("provider").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("reply").GetString()));
        Assert.True(json.TryGetProperty("confidence", out _));
        Assert.True(json.TryGetProperty("evidenceRefs", out _));
    }

    [Fact]
    public async Task Rebaseline_requires_consent()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<PersonalImprovementService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RebaselineAsync(ws, userId, consent: false, subjectKey: null, CancellationToken.None));
    }

    [Fact]
    public async Task Coach_rejects_foreign_thread_and_disabled_ai()
    {
        var (spA, wsA, userA) = await CreateAsync();
        var (spB, wsB, userB) = await CreateAsync();
        await using var scopeA = spA.CreateAsyncScope();
        await using var scopeB = spB.CreateAsyncScope();
        var coachA = scopeA.ServiceProvider.GetRequiredService<AiCoachService>();
        var coachB = scopeB.ServiceProvider.GetRequiredService<AiCoachService>();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var first = JsonSerializer.SerializeToElement(
            await coachA.ChatAsync(wsA, userA, "سلام", null, CancellationToken.None));
        var threadId = first.GetProperty("threadId").GetGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coachB.ChatAsync(wsB, userB, "دزدی thread", threadId, CancellationToken.None));

        dbA.AiWorkspaceSettings.First(s => s.WorkspaceId == wsA).Enabled = false;
        await dbA.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coachA.ChatAsync(wsA, userA, "باید رد شود", null, CancellationToken.None));
        Assert.Equal(0, await dbB.CoachThreads.CountAsync(t => t.Id == threadId));
    }

    [Fact]
    public async Task Coach_discards_provider_fabricated_evidenceRefs()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<PersonalImprovementService>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<AiContextBuilder>();
        services.AddScoped<AiCoachService>();
        services.AddScoped<OutboxPublisher>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(Options.Create(new KonkoorHelp.Api.Services.Integrations.IntegrationsOptions
        {
            WebhookProtectionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        }));
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookSecretProtector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookDnsResolver, KonkoorHelp.Api.Services.Integrations.SystemWebhookDnsResolver>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector, KonkoorHelp.Api.Services.Integrations.TcpWebhookSocketConnector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookUrlSafety>();
        services.AddScoped<KonkoorHelp.Api.Services.Integrations.WebhookService>();
        services.AddHttpClient(nameof(KonkoorHelp.Api.Services.Integrations.WebhookService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
            KonkoorHelp.Api.Services.Integrations.WebhookSocketsHttpHandlerFactory.Create(
                sp.GetRequiredService<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector>()));
        services.AddSingleton<IOptions<AiOptions>>(Options.Create(new AiOptions { DefaultProvider = "fabricating" }));
        services.AddSingleton<IAiProvider, FabricatingAiProvider>();
        services.AddSingleton<IAiProvider, HeuristicAiProvider>();
        services.AddSingleton<IAiProviderResolver, AiProviderResolver>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "fab",
            NormalizedUsername = "fab",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = WorkspaceType.Personal,
            Name = "f",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = user.Id,
            Role = WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = ws,
            FeatureFlags = """{"ai":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        db.AiWorkspaceSettings.Add(new AiWorkspaceSettings
        {
            WorkspaceId = ws,
            Enabled = true,
            ProviderCode = "fabricating",
            AllowUnderAge = true,
            UpdatedAt = DateTime.UtcNow,
        });
        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = user.Id,
            Subject = "ریاضی",
            Topic = "حد",
            ScoreEma = 0.3,
            EvidenceCount = 2,
            LastEvidenceAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();
        var json = JsonSerializer.SerializeToElement(
            await coach.ChatAsync(ws, user.Id, "بگو", null, CancellationToken.None));
        Assert.Equal("serverFactsOnly", json.GetProperty("evidencePolicy").GetString());
        var refs = json.GetProperty("evidenceRefs").EnumerateArray().Select(item => item.GetRawText()).ToList();
        Assert.DoesNotContain(refs, text => text.Contains("OTHER_WORKSPACE", StringComparison.Ordinal));
        Assert.Equal("medium", json.GetProperty("confidence").GetString()); // server capped below provider "high"
    }

    [Fact]
    public async Task Sparkline_declares_snapshot_source_not_historical_mastery()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<PersonalImprovementService>();
        await SeedReadyEvidenceAsync(db, ws, userId);
        var json = JsonSerializer.SerializeToElement(await svc.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("weeklySnapshotsOnRead", json.GetProperty("sparklineSource").GetString());
        Assert.Contains("اسپارک", json.GetProperty("sparklineNote").GetString()!);
    }

    private sealed class FabricatingAiProvider : IAiProvider
    {
        public string Code => "fabricating";

        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(
                true,
                """{"reply":"جعلی","confidence":"high","evidenceRefs":[{"type":"MasteryNode","id":"OTHER_WORKSPACE","subject":"دزدیده‌شده"}]}""",
                null,
                Code));
    }

    private static async Task SeedReadyEvidenceAsync(AppDbContext db, Guid ws, int userId)
    {
        var start = DateTime.UtcNow.AddDays(-8);
        for (var i = 0; i < 5; i++)
        {
            db.MasteryNodes.Add(new MasteryNode
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                UserId = userId,
                Subject = "فیزیک",
                Topic = $"n{i}",
                ScoreEma = 0.5,
                EvidenceCount = 2,
                LastEvidenceAt = start.AddDays(i),
                UpdatedAt = start.AddDays(i),
            });
        }
        await db.SaveChangesAsync();
    }
}
