using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace KonkoorHelp.Tests;

public class GamificationEngineTests
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
        services.AddScoped<KonkoorHelp.Api.Services.Integrations.WebhookService>();
        services.AddHttpClient(nameof(KonkoorHelp.Api.Services.Integrations.WebhookService));
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new UserAccount
            {
                Username = "tester",
                NormalizedUsername = "tester",
                PasswordHash = "x",
                CreatedAt = DateTime.UtcNow,
                PlatformRole = 100,
            };
            db.Users.Add(user);
            db.SaveChanges();

            var workspaceId = Guid.NewGuid();
            db.Workspaces.Add(new Workspace
            {
                Id = workspaceId,
                Type = WorkspaceType.Personal,
                Name = "فضای شخصی",
                OwnerUserId = user.Id,
                Status = WorkspaceStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.WorkspaceSettings.Add(new WorkspaceSettings
            {
                WorkspaceId = workspaceId,
                Data = "{}",
                LocaleDefault = "fa-IR",
                TimeZone = "Asia/Tehran",
                FeatureFlags = """{"gamification":true}""",
                UpdatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
            DefaultGamificationSeed.EnsureAsync(db).GetAwaiter().GetResult();
            return (sp, workspaceId, user.Id);
        }
    }

    private static async Task ProcessAllAsync(IServiceProvider sp)
    {
        for (var i = 0; i < 8; i++)
        {
            await using var scope = sp.CreateAsyncScope();
            var pipeline = scope.ServiceProvider.GetRequiredService<GamificationPipeline>();
            var n = await pipeline.ProcessPendingAsync(CancellationToken.None);
            if (n == 0) break;
        }
    }

    [Fact]
    public async Task Duplicate_outbox_publish_awards_xp_once()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:s1:completed",
            new { sessionId = "s1", durationMinutes = 30 }, CancellationToken.None);
        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:s1:completed",
            new { sessionId = "s1", durationMinutes = 30 }, CancellationToken.None);

        Assert.Equal(1, await db.DomainOutbox.CountAsync());
        await ProcessAllAsync(sp);

        await using var verify = sp.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await verifyDb.DomainOutbox.SingleAsync();
        Assert.True(row.Status == OutboxStatus.Processed, $"outbox={row.Status} err={row.LastError}");
        Assert.Equal(1, await verifyDb.XpLedger.CountAsync(item => item.Kind == LedgerKind.Earn));
        var stats = await verifyDb.UserGameStats.SingleAsync();
        Assert.Equal(10, stats.XpTotal);
    }

    [Fact]
    public async Task Daily_xp_cap_is_enforced()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var profiles = scope.ServiceProvider.GetRequiredService<GamificationProfileService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await profiles.ResolveAsync(workspaceId, CancellationToken.None);
        var tracked = await db.GamificationProfiles.FirstAsync(item => item.Id == profile.Id);
        tracked.DailyXpCap = 15;
        await db.SaveChangesAsync();

        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        for (var i = 0; i < 3; i++)
        {
            await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, $"studySession:s{i}:completed",
                new { sessionId = $"s{i}", durationMinutes = 30 }, CancellationToken.None);
        }

        await ProcessAllAsync(sp);
        await using var verify = sp.CreateAsyncScope();
        var stats = await verify.ServiceProvider.GetRequiredService<AppDbContext>().UserGameStats.SingleAsync();
        Assert.Equal(15, stats.XpTotal);
    }

    [Fact]
    public async Task Level_transitions_create_notification()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        // 100 XP needed for level 2 → 10 sessions of 30m = 100 XP
        for (var i = 0; i < 10; i++)
        {
            await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, $"studySession:lvl{i}:completed",
                new { sessionId = $"lvl{i}", durationMinutes = 30 }, CancellationToken.None);
        }

        await ProcessAllAsync(sp);
        await using var verify = sp.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var stats = await db.UserGameStats.SingleAsync();
        Assert.True(stats.Level >= 2);
        Assert.Contains(await db.GameNotifications.ToListAsync(), item => item.Kind == GameNotificationKind.LevelUp);
    }

    [Fact]
    public async Task Badge_awarded_only_once()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:first:completed",
            new { sessionId = "first", durationMinutes = 30 }, CancellationToken.None);
        await ProcessAllAsync(sp);
        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:second:completed",
            new { sessionId = "second", durationMinutes = 30 }, CancellationToken.None);
        await ProcessAllAsync(sp);

        await using var verify = sp.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstStudy = await db.Badges.SingleAsync(item => item.Code == "first_study");
        Assert.Equal(1, await db.UserBadges.CountAsync(item => item.BadgeId == firstStudy.Id));
    }

    [Fact]
    public async Task Quest_expires_without_claim_reward()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quest = new QuestDefinition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = "منقضی",
            Description = "تست",
            Category = "study",
            Kind = QuestKind.Daily,
            TargetValue = 1,
            RewardXp = 99,
            RewardCoins = 9,
            CompletionCondition = "session_count",
            Status = QuestStatus.Active,
            EndsAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        };
        db.Quests.Add(quest);
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            QuestId = quest.Id,
            Progress = 0,
            Status = QuestStatus.Active,
            UpdatedAt = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:exp:completed",
            new { sessionId = "exp", durationMinutes = 30 }, CancellationToken.None);
        await ProcessAllAsync(sp);

        await using var verify = sp.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var progress = await verifyDb.UserQuestProgress.SingleAsync(item => item.QuestId == quest.Id);
        Assert.Equal(QuestStatus.Expired, progress.Status);
        Assert.DoesNotContain(await verifyDb.XpLedger.ToListAsync(), item => item.Reason.Contains("quest") && item.Delta == 99);
    }

    [Fact]
    public void Streak_local_date_uses_tehran_not_utc_midnight()
    {
        // 2026-11-06 21:30 UTC = 2026-11-07 01:00 Tehran (UTC+3:30)
        var utc = new DateTime(2026, 11, 6, 21, 30, 0, DateTimeKind.Utc);
        var local = StreakEngine.ToLocalDate(utc, "Asia/Tehran");
        Assert.Equal(new DateOnly(2026, 11, 7), local);
    }

    [Fact]
    public async Task Rest_day_keeps_streak_bridge()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var streaks = scope.ServiceProvider.GetRequiredService<StreakEngine>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = StreakEngine.ToLocalDate(DateTime.UtcNow, "Asia/Tehran");
        db.StreakDays.Add(new StreakDay
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            LocalDate = today.AddDays(-1),
            Kind = StreakDayKind.Study,
            Minutes = 30,
            CreatedAt = DateTime.UtcNow,
        });
        db.UserGameStats.Add(new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Level = 1,
            StreakCurrent = 1,
            StreakBest = 1,
            StreakFreezes = 2,
            TimeZone = "Asia/Tehran",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await streaks.SetRestDayAsync(workspaceId, userId, today, CancellationToken.None);
        var day = await db.StreakDays.SingleAsync(item => item.LocalDate == today);
        Assert.Equal(StreakDayKind.Rest, day.Kind);
    }

    [Fact]
    public async Task School_profile_can_differ_from_personal_defaults()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var system = await db.GamificationProfiles.SingleAsync(item => item.Code == "default");
        var schoolProfile = new GamificationProfile
        {
            Id = Guid.NewGuid(),
            Code = "school-a",
            Name = "مدرسه",
            IsSystem = false,
            RulesJson = JsonSerializer.Serialize(new ProfileRulesDocument
            {
                Awards = [new XpAwardRule { Code = "study_boost", On = DomainEventTypes.StudySessionCompleted, Xp = 50, PerMinutes = 30 }],
            }, GamificationJson.Options),
            LevelsJson = system.LevelsJson,
            StreakJson = system.StreakJson,
            DailyXpCap = 500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.GamificationProfiles.Add(schoolProfile);
        var settings = await db.WorkspaceSettings.SingleAsync(item => item.WorkspaceId == workspaceId);
        settings.GamificationProfileId = schoolProfile.Id;
        await db.SaveChangesAsync();

        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
        await outbox.PublishAsync(workspaceId, userId, DomainEventTypes.StudySessionCompleted, "studySession:school:completed",
            new { sessionId = "school", durationMinutes = 30 }, CancellationToken.None);
        await ProcessAllAsync(sp);

        await using var verify = sp.CreateAsyncScope();
        var stats = await verify.ServiceProvider.GetRequiredService<AppDbContext>().UserGameStats.SingleAsync();
        Assert.Equal(50, stats.XpTotal);
    }

    [Fact]
    public void Level_progress_math_is_stable()
    {
        var levels = DefaultGamificationSeed.DefaultLevels();
        var (current, next, progress, need) = LevelEngine.Progress(150, levels);
        Assert.Equal(2, current.Level);
        Assert.Equal(3, next!.Level);
        Assert.Equal(50, progress);
        Assert.Equal(200, need);
    }
}
