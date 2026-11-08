using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

/// <summary>Phase 14 Beta release smoke — critical learner paths stay green.</summary>
public class Phase14ReleaseSmokeTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateAsync(WorkspaceType type = WorkspaceType.Personal)
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<XpEngine>();
        services.AddScoped<LevelEngine>();
        services.AddScoped<BadgeEngine>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<StreakEngine>();
        services.AddScoped<GamificationQueryService>();
        services.AddScoped<GamificationPipeline>();
        services.AddScoped<DailyMissionService>();
        services.AddScoped<JourneyService>();
        services.AddScoped<PersonalImprovementService>();
        services.AddScoped<LearnerHomeService>();
        services.AddScoped<LeaderboardService>();
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
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "beta" + Guid.NewGuid().ToString("N")[..6],
            NormalizedUsername = Guid.NewGuid().ToString("N"),
            PasswordHash = "x",
            DisplayName = "بتا",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = type,
            Name = type == WorkspaceType.School ? "مدرسه بتا" : "شخصی بتا",
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
            Role = type == WorkspaceType.School ? WorkspaceMemberRole.SchoolOwner : WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = ws,
            FeatureFlags = """{"gamification":true,"ai":true,"aiCoach":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        return (sp, ws, user.Id);
    }

    [Fact]
    public async Task Learner_home_compose_exposes_beta_contracts()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var home = scope.ServiceProvider.GetRequiredService<LearnerHomeService>();

        var json = JsonSerializer.SerializeToElement(await home.GetHomeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("server", json.GetProperty("streakSource").GetString());
        Assert.Equal("private", json.GetProperty("privacy").GetProperty("leaderboardDefault").GetString());
        Assert.Equal("self-first", json.GetProperty("privacy").GetProperty("improvementView").GetString());
        Assert.True(json.TryGetProperty("profile", out _));
        Assert.True(json.TryGetProperty("dailyMissions", out _));
        Assert.True(json.TryGetProperty("journeySummary", out _));
        Assert.True(json.TryGetProperty("improvementPulse", out _));
    }

    [Fact]
    public async Task Privacy_default_keeps_new_member_off_leaderboard()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var boards = scope.ServiceProvider.GetRequiredService<LeaderboardService>();

        db.XpLedger.Add(new XpLedger
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Delta = 50,
            BalanceAfter = 50,
            Reason = "beta",
            Kind = LedgerKind.Earn,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var privacy = JsonSerializer.SerializeToElement(await boards.GetPrivacyAsync(ws, userId, CancellationToken.None));
        Assert.False(privacy.GetProperty("leaderboardVisible").GetBoolean());

        var board = JsonSerializer.SerializeToElement(await boards.GetBoardAsync(
            ws, LeaderboardMetric.WeeklyXp, "week", null, null, 1, 20, CancellationToken.None));
        Assert.Equal(0, board.GetProperty("total").GetInt32());
        Assert.Equal(0, board.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public async Task Journey_and_improvement_smoke_for_personal_workspace()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();
        var improvement = scope.ServiceProvider.GetRequiredService<PersonalImprovementService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Subject = "ریاضی",
            Topic = "جبر",
            ScoreEma = 0.4,
            EvidenceCount = 1,
            LastEvidenceAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var map = JsonSerializer.SerializeToElement(await journeys.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.True(map.GetProperty("isBootstrap").GetBoolean());
        Assert.True(map.GetProperty("regions").GetArrayLength() >= 1);

        var improve = JsonSerializer.SerializeToElement(await improvement.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.Equal("Collecting", improve.GetProperty("composite").GetProperty("status").GetString());
    }
}
