using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

public class LearningIntelligenceTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<XpEngine>();
        services.AddScoped<LevelEngine>();
        services.AddScoped<BadgeEngine>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<StreakEngine>();
        services.AddScoped<GamificationQueryService>();
        services.AddScoped<GamificationPipeline>();
        services.AddScoped<MasteryService>();
        services.AddScoped<SpacedRepetitionService>();
        services.AddScoped<AssessmentService>();
        services.AddScoped<AiContextBuilder>();
        services.AddScoped<AiCoachService>();
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
        services.AddSingleton<IAiProvider, FailingAiProvider>();
        services.AddSingleton<IAiProviderResolver, AiProviderResolver>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "learner",
            NormalizedUsername = "learner",
            PasswordHash = "x",
            DisplayName = "یادگیرنده",
            CreatedAt = DateTime.UtcNow,
            PlatformRole = 100,
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
            FeatureFlags = """{"gamification":true,"ai":true,"aiCoach":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        await scope.ServiceProvider.GetRequiredService<SpacedRepetitionService>().EnsureDefaultProfileAsync();
        return (sp, workspaceId, user.Id);
    }

    [Fact]
    public async Task Assessment_scores_mcq_and_truefalse_and_updates_mastery()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var assessments = scope.ServiceProvider.GetRequiredService<AssessmentService>();
        var mastery = scope.ServiceProvider.GetRequiredService<MasteryService>();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await assessments.CreateAsync(
            workspaceId,
            userId,
            "کوییز موضوعی",
            null,
            AssessmentKind.TopicQuiz,
            "ریاضی",
            "فصل۱",
            "معادله",
            null,
            70,
            null,
            [
                new QuestionInput(QuestionType.MultipleChoice, "۲+۲؟", """["3","4"]""", "\"4\"", 1, "ریاضی", "فصل۱", "معادله"),
                new QuestionInput(QuestionType.TrueFalse, "۲ زوج است", "[]", "true", 1, "ریاضی", "فصل۱", "معادله"),
            ],
            true,
            CancellationToken.None)));
        var assessmentId = created.RootElement.GetProperty("id").GetGuid();
        var detail = JsonDocument.Parse(JsonSerializer.Serialize(await assessments.GetAsync(workspaceId, assessmentId, true, CancellationToken.None)));
        var q1 = detail.RootElement.GetProperty("questions")[0].GetProperty("id").GetGuid();
        var q2 = detail.RootElement.GetProperty("questions")[1].GetProperty("id").GetGuid();

        var started = JsonDocument.Parse(JsonSerializer.Serialize(await assessments.StartAttemptAsync(workspaceId, userId, assessmentId, CancellationToken.None)));
        var attemptId = started.RootElement.GetProperty("attemptId").GetGuid();
        var result = JsonDocument.Parse(JsonSerializer.Serialize(await assessments.SubmitAttemptAsync(
            workspaceId,
            userId,
            attemptId,
            [
                new AnswerInput(q1, "\"4\"", 12),
                new AnswerInput(q2, "true", 8),
            ],
            CancellationToken.None)));

        Assert.Equal(100, result.RootElement.GetProperty("percent").GetDouble());
        var map = JsonDocument.Parse(JsonSerializer.Serialize(await mastery.GetMapAsync(workspaceId, userId, "ریاضی", CancellationToken.None)));
        Assert.True(map.RootElement.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Mastery_level_ignores_duration_only_and_needs_evidence()
    {
        var node = new MasteryNode { EvidenceCount = 0, ScoreEma = 1, LastEvidenceAt = DateTime.UtcNow };
        Assert.Equal(MasteryLevel.NotStarted, MasteryService.ComputeLevel(node));

        node.EvidenceCount = 1;
        node.ScoreEma = 0.95;
        Assert.Equal(MasteryLevel.Learning, MasteryService.ComputeLevel(node));

        node.EvidenceCount = 5;
        node.ReviewSuccessCount = 2;
        node.ScoreEma = 0.92;
        node.LastEvidenceAt = DateTime.UtcNow;
        Assert.Equal(MasteryLevel.Mastered, MasteryService.ComputeLevel(node));
    }

    [Fact]
    public async Task Spaced_repetition_schedules_next_review_and_keeps_history()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var srs = scope.ServiceProvider.GetRequiredService<SpacedRepetitionService>();
        var mastery = scope.ServiceProvider.GetRequiredService<MasteryService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cardJson = JsonDocument.Parse(JsonSerializer.Serialize(await srs.EnsureCardAsync(workspaceId, userId, "فیزیک", "۱", "نیرو", CancellationToken.None)));
        var cardId = cardJson.RootElement.GetProperty("id").GetGuid();
        await srs.ReviewAsync(workspaceId, userId, cardId, ReviewOutcome.Good, 3, 4, mastery, CancellationToken.None);

        var card = await db.SpacedRepetitionCards.SingleAsync();
        Assert.True(card.NextReviewDate > DateOnly.FromDateTime(DateTime.UtcNow.Date) || card.IntervalDays >= 1);
        Assert.Equal(1, await db.SpacedRepetitionReviews.CountAsync());
        Assert.False(string.IsNullOrWhiteSpace((await db.SpacedRepetitionReviews.SingleAsync()).StateBeforeJson));
    }

    [Fact]
    public async Task Ai_provider_failure_surfaces_clean_error()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();
        await coach.UpdateSettingsAsync(workspaceId, true, "failing", null, false, 13, CancellationToken.None);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coach.AnalyzeAsync(workspaceId, userId, null, CancellationToken.None));
        Assert.Contains("fail", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ai_authorization_blocks_cross_user_access()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var context = scope.ServiceProvider.GetRequiredService<AiContextBuilder>();

        var other = new UserAccount
        {
            Username = "other",
            NormalizedUsername = "other",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(other);
        await db.SaveChangesAsync();
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = other.Id,
            Role = WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // demote actor to student without elevated role: recreate membership as student already is
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            context.EnsureAuthorizedAsync(workspaceId, other.Id, userId, CancellationToken.None));
    }

    [Fact]
    public async Task Ai_recommendations_require_approval_and_include_facts()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();
        await coach.UpdateSettingsAsync(workspaceId, true, "heuristic", null, false, 13, CancellationToken.None);

        var analysis = JsonDocument.Parse(JsonSerializer.Serialize(await coach.AnalyzeAsync(workspaceId, userId, null, CancellationToken.None)));
        Assert.True(analysis.RootElement.TryGetProperty("facts", out _));
        Assert.Contains("facts", analysis.RootElement.GetProperty("note").GetString()!, StringComparison.OrdinalIgnoreCase);

        var recs = analysis.RootElement.GetProperty("recommendations");
        Assert.True(recs.GetArrayLength() > 0);
        var id = recs[0].GetProperty("id").GetGuid();
        var resolved = JsonDocument.Parse(JsonSerializer.Serialize(
            await coach.ResolveRecommendationAsync(workspaceId, userId, id, true, null, CancellationToken.None)));
        Assert.Equal("Approved", resolved.RootElement.GetProperty("status").GetString());

        var plan = JsonDocument.Parse(JsonSerializer.Serialize(
            await coach.SuggestPlanAsync(workspaceId, userId, 90, "[]", CancellationToken.None)));
        Assert.Equal("Suggested", plan.RootElement.GetProperty("status").GetString());
        var planId = plan.RootElement.GetProperty("id").GetGuid();
        var approved = JsonDocument.Parse(JsonSerializer.Serialize(
            await coach.ResolvePlanAsync(workspaceId, userId, planId, true, """[{"day":"یکشنبه","minutes":40}]""", CancellationToken.None)));
        Assert.Equal("Approved", approved.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ai_disabled_blocks_coach()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var coach = scope.ServiceProvider.GetRequiredService<AiCoachService>();
        await coach.UpdateSettingsAsync(workspaceId, false, "heuristic", null, false, 13, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coach.WeeklyReportAsync(workspaceId, userId, null, CancellationToken.None));
    }

    private sealed class FailingAiProvider : IAiProvider
    {
        public string Code => "failing";
        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AiCompletionResult(false, "", "provider failed", Code));
    }
}
