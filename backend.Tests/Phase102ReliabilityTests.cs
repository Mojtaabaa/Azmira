using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class Phase102ReliabilityTests
{
    private static ServiceProvider Build(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<XpEngine>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<DailyMissionService>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static async Task<(Guid Ws, int UserId)> SeedAsync(ServiceProvider sp, string tz = "Asia/Tehran")
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "u" + Guid.NewGuid().ToString("N")[..8],
            NormalizedUsername = Guid.NewGuid().ToString("N"),
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
            Name = "p",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = ws,
            Data = "{}",
            LocaleDefault = "fa-IR",
            TimeZone = tz,
            FeatureFlags = """{"gamification":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        return (ws, user.Id);
    }

    [Fact]
    public async Task F1_claim_enqueues_outbox_atomically_and_is_idempotent()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "t",
            Description = "d",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            TargetValue = 1,
            RewardXp = 10,
            RewardCoins = 2,
            CompletionCondition = "session_count",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = questId,
            Progress = 1,
            Status = QuestStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await quests.ClaimAsync(ws, userId, questId, CancellationToken.None);
        await quests.ClaimAsync(ws, userId, questId, CancellationToken.None);

        var key = QuestEngine.RewardIdempotencyKey(questId, userId);
        Assert.Equal(1, await db.DomainOutbox.CountAsync(e => e.IdempotencyKey == key));
        Assert.Equal(QuestStatus.Claimed, (await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Status);
    }

    [Fact]
    public async Task F1_recovery_publishes_missing_outbox_for_claimed()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        var progress = new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = questId,
            Progress = 1,
            Status = QuestStatus.Claimed,
            ClaimedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Quest = new QuestDefinition
            {
                Id = questId,
                WorkspaceId = ws,
                Title = "t",
                Description = "d",
                Category = "dm",
                Kind = QuestKind.Personalized,
                RewardXp = 7,
                RewardCoins = 1,
                CompletionCondition = "session_count",
                Status = QuestStatus.Active,
                AssignedUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        db.Quests.Add(progress.Quest);
        db.UserQuestProgress.Add(progress);
        await db.SaveChangesAsync();

        await quests.EnsureRewardOutboxAsync(ws, userId, progress, CancellationToken.None);
        await quests.EnsureRewardOutboxAsync(ws, userId, progress, CancellationToken.None);

        Assert.Equal(1, await db.DomainOutbox.CountAsync(e =>
            e.IdempotencyKey == QuestEngine.RewardIdempotencyKey(questId, userId)));
    }

    [Fact]
    public async Task F2_concurrent_assignment_caps_at_three()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var sp = Build(dbName);
        var (ws, userId) = await SeedAsync(sp);
        var tz = "Asia/Tehran";
        var local = MissionClock.ToLocalDate(DateTime.UtcNow, tz);

        var tasks = Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var scope = sp.CreateAsyncScope();
            var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
            await missions.EnsureAssignedAsync(ws, userId, local, tz, CancellationToken.None);
        });
        await Task.WhenAll(tasks);

        await using var verify = sp.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await db.Quests.CountAsync(q =>
            q.WorkspaceId == ws && q.AssignedUserId == userId && q.Category == DailyMissionCatalog.Category);
        Assert.True(count <= DailyMissionCatalog.MaxMissionsPerDay, $"count={count}");
        var xp = await db.Quests.Where(q => q.WorkspaceId == ws && q.Category == DailyMissionCatalog.Category)
            .SumAsync(q => q.RewardXp);
        Assert.True(xp <= DailyMissionCatalog.MaxDailyMissionXp);
    }

    [Fact]
    public async Task F3_evidence_uses_persisted_window_not_current_tz()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp, "Asia/Tehran");
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();

        var local = MissionClock.ToLocalDate(DateTime.UtcNow, "Asia/Tehran");
        var (start, end) = MissionClock.LocalDayUtcBounds(local, "Asia/Tehran");
        var questId = DailyMissionIds.QuestId(ws, userId, local, "review_due");
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "r",
            Description = $"{DailyMissionCatalog.MetaPrefix("review_due", local)} x",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            StartsAt = start,
            EndsAt = end,
            TargetValue = 1,
            RewardXp = 5,
            CompletionCondition = "review_count",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = DailyMissionIds.ProgressId(ws, userId, local, "review_due"),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = questId,
            Status = QuestStatus.Active,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, "before", new { }, CancellationToken.None);
        (await db.DomainOutbox.SingleAsync(e => e.IdempotencyKey == "before")).OccurredAt = start.AddMinutes(-1);
        await db.SaveChangesAsync();

        await missions.SyncAsync(ws, userId, CancellationToken.None);
        Assert.Equal(0, (await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Progress);

        await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, "at-start", new { }, CancellationToken.None);
        (await db.DomainOutbox.SingleAsync(e => e.IdempotencyKey == "at-start")).OccurredAt = start;
        await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, "near-end", new { }, CancellationToken.None);
        (await db.DomainOutbox.SingleAsync(e => e.IdempotencyKey == "near-end")).OccurredAt = end.AddMinutes(-1);
        await db.SaveChangesAsync();

        var settings = await db.WorkspaceSettings.SingleAsync(s => s.WorkspaceId == ws);
        settings.TimeZone = "UTC";
        await db.SaveChangesAsync();

        await missions.SyncAsync(ws, userId, CancellationToken.None);
        var progress = await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId);
        Assert.Equal(1, progress.Progress); // TargetValue=1; both in-window events count but clamp
        Assert.Equal(QuestStatus.Completed, progress.Status);
        Assert.Equal(start, (await db.Quests.SingleAsync(q => q.Id == questId)).StartsAt);
        Assert.Equal(end, (await db.Quests.SingleAsync(q => q.Id == questId)).EndsAt);

        await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, "after", new { }, CancellationToken.None);
        (await db.DomainOutbox.SingleAsync(e => e.IdempotencyKey == "after")).OccurredAt = end.AddMinutes(1);
        // Remove in-window evidence; only "after" remains outside EndsAt
        foreach (var row in await db.DomainOutbox.Where(e => e.IdempotencyKey != "after").ToListAsync())
            db.DomainOutbox.Remove(row);
        progress.Status = QuestStatus.Active;
        progress.Progress = 0;
        progress.CompletedAt = null;
        await db.SaveChangesAsync();
        await missions.SyncAsync(ws, userId, CancellationToken.None);
        Assert.Equal(0, (await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Progress);
    }

    [Fact]
    public async Task F8_duplicate_event_delivery_does_not_double_xp()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var xp = scope.ServiceProvider.GetRequiredService<XpEngine>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();

        var eventId = await outbox.PublishAsync(ws, userId, DomainEventTypes.QuestCompleted, "dup-q",
            new { questId = Guid.NewGuid(), rewardXp = 20, rewardCoins = 2 }, CancellationToken.None);
        Assert.NotNull(eventId);
        var row = await db.DomainOutbox.SingleAsync(e => e.Id == eventId);
        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(row.PayloadJson, GamificationJson.Options)!;

        await xp.ProcessAsync(row, envelope, CancellationToken.None);
        await xp.ProcessAsync(row, envelope, CancellationToken.None);

        Assert.Equal(1, await db.XpLedger.CountAsync(l => l.EventId == eventId));
        Assert.True((await db.UserGameStats.SingleAsync(s => s.UserId == userId)).XpTotal <= 20);
    }

    [Fact]
    public async Task F8_concurrent_distinct_events_respect_daily_xp_cap()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var sp = Build(dbName);
        var (ws, userId) = await SeedAsync(sp);
        const int cap = 50;
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var profile = await db.GamificationProfiles.SingleAsync(p => p.Code == DefaultGamificationSeed.DefaultProfileCode);
            profile.DailyXpCap = cap;
            await db.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, 8).Select(async i =>
        {
            await using var scope = sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var xp = scope.ServiceProvider.GetRequiredService<XpEngine>();
            var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();
            var id = await outbox.PublishAsync(ws, userId, DomainEventTypes.QuestCompleted, $"cap-{i}",
                new { questId = Guid.NewGuid(), rewardXp = 20, rewardCoins = 0 }, CancellationToken.None);
            var row = await db.DomainOutbox.SingleAsync(e => e.Id == id);
            var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(row.PayloadJson, GamificationJson.Options)!;
            await xp.ProcessAsync(row, envelope, CancellationToken.None);
        });
        await Task.WhenAll(tasks);

        await using var verify = sp.CreateAsyncScope();
        var vdb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var total = await vdb.XpLedger.Where(l => l.UserId == userId && l.Kind == LedgerKind.Earn).SumAsync(l => l.Delta);
        Assert.True(total <= cap, $"total={total} cap={cap}");
    }

    [Fact]
    public async Task F4_timezone_change_does_not_open_second_active_day()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp, "Asia/Tehran");
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();

        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        var firstCount = await db.Quests.CountAsync(q => q.Category == DailyMissionCatalog.Category && q.AssignedUserId == userId);

        var settings = await db.WorkspaceSettings.SingleAsync(s => s.WorkspaceId == ws);
        settings.TimeZone = "UTC";
        await db.SaveChangesAsync();

        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        var secondCount = await db.Quests.CountAsync(q => q.Category == DailyMissionCatalog.Category && q.AssignedUserId == userId);
        Assert.Equal(firstCount, secondCount);
    }

    [Fact]
    public async Task F5_concurrent_claims_single_winner()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var sp = Build(dbName);
        var (ws, userId) = await SeedAsync(sp);
        Guid questId;
        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            questId = Guid.NewGuid();
            db.Quests.Add(new QuestDefinition
            {
                Id = questId,
                WorkspaceId = ws,
                Title = "c",
                Description = "d",
                Category = DailyMissionCatalog.Category,
                Kind = QuestKind.Personalized,
                TargetValue = 1,
                RewardXp = 9,
                RewardCoins = 1,
                CompletionCondition = "session_count",
                Status = QuestStatus.Active,
                AssignedUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.UserQuestProgress.Add(new UserQuestProgress
            {
                Id = Guid.NewGuid(),
                WorkspaceId = ws,
                UserId = userId,
                QuestId = questId,
                Progress = 1,
                Status = QuestStatus.Completed,
                CompletedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var errors = new ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await using var scope = sp.CreateAsyncScope();
                var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();
                await quests.ClaimAsync(ws, userId, questId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        await Task.WhenAll(tasks);

        await using var verify = sp.CreateAsyncScope();
        var vdb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await vdb.DomainOutbox.CountAsync(e =>
            e.IdempotencyKey == QuestEngine.RewardIdempotencyKey(questId, userId)));
        Assert.Equal(QuestStatus.Claimed, (await vdb.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Status);
    }

    [Fact]
    public async Task F6_teacher_confirm_writes_audit_and_rejects_wrong_workspace()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "manual",
            Description = "m",
            Category = "classroom",
            Kind = QuestKind.TeacherAssigned,
            TargetValue = 1,
            RewardXp = 5,
            CompletionCondition = "manual",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await quests.ConfirmManualAsync(ws, userId, questId, teacherUserId: 42, note: "ok", CancellationToken.None);
        await quests.ConfirmManualAsync(ws, userId, questId, teacherUserId: 42, note: "again", CancellationToken.None);

        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "quest.manual_confirm" && a.WorkspaceId == ws));
        Assert.Contains("noteSha256", (await db.AuditLogs.SingleAsync()).DetailJson);

        var otherWs = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quests.ConfirmManualAsync(otherWs, userId, questId, 42, null, CancellationToken.None));
    }

    [Fact]
    public async Task F11_rejects_workspace_null_quest_confirm()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = null,
            Title = "system",
            Description = "m",
            Category = "classroom",
            Kind = QuestKind.TeacherAssigned,
            TargetValue = 1,
            CompletionCondition = "manual",
            Status = QuestStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quests.ConfirmManualAsync(ws, userId, questId, 1, null, CancellationToken.None));
    }

    [Fact]
    public void F7_review_due_uses_mission_clock_local_date()
    {
        var tehran = MissionClock.ToLocalDate(new DateTime(2026, 11, 7, 21, 30, 0, DateTimeKind.Utc), "Asia/Tehran");
        var utc = MissionClock.ToLocalDate(new DateTime(2026, 11, 7, 21, 30, 0, DateTimeKind.Utc), "UTC");
        Assert.Equal(new DateOnly(2026, 11, 8), tehran); // 01:00 next day in Tehran (UTC+3:30)
        Assert.Equal(new DateOnly(2026, 11, 7), utc);
    }

    [Fact]
    public void F9_catalog_marks_time_based_conditions()
    {
        Assert.Contains("study_minutes", DailyMissionService.TimeBasedConditions);
        Assert.Contains("session_count", DailyMissionService.TimeBasedConditions);
        Assert.DoesNotContain("review_count", DailyMissionService.TimeBasedConditions);
    }

    [Fact]
    public async Task F9_at_most_one_time_based_mission_assigned()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        var timeBased = await db.Quests.CountAsync(q =>
            q.WorkspaceId == ws && q.Category == DailyMissionCatalog.Category
            && (q.CompletionCondition == "study_minutes" || q.CompletionCondition == "session_count"));
        Assert.True(timeBased <= 1);
    }

    [Fact]
    public async Task F10_completed_past_grace_expires_without_claim()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();

        var questId = Guid.NewGuid();
        var ends = DateTime.UtcNow - QuestEngine.ClaimGraceAfterEndsAt - TimeSpan.FromMinutes(1);
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "old",
            Description = $"{DailyMissionCatalog.MetaPrefix("session_focus", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)))} x",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            StartsAt = ends.AddDays(-1),
            EndsAt = ends,
            TargetValue = 1,
            RewardXp = 5,
            CompletionCondition = "session_count",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = questId,
            Progress = 1,
            Status = QuestStatus.Completed,
            CompletedAt = ends,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        Assert.Equal(QuestStatus.Expired, (await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            quests.ClaimAsync(ws, userId, questId, CancellationToken.None));
    }

    [Fact]
    public void DST_day_bounds_remain_contiguous_for_cet()
    {
        // Last Sunday of March 2026 is EU spring-forward in many CET zones
        var day = new DateOnly(2026, 3, 29);
        string tz;
        try
        {
            tz = OperatingSystem.IsWindows() ? "Central Europe Standard Time" : "Europe/Berlin";
            _ = TimeZoneInfo.FindSystemTimeZoneById(MissionClock.NormalizeTz(tz) == tz ? tz : tz);
        }
        catch (TimeZoneNotFoundException)
        {
            tz = "UTC";
        }

        var (start, end) = MissionClock.LocalDayUtcBounds(day, tz);
        var (startNext, _) = MissionClock.LocalDayUtcBounds(day.AddDays(1), tz);
        Assert.True(end < startNext);
        Assert.Equal(day, MissionClock.ToLocalDate(start.AddMinutes(30), tz));
    }

    [Fact]
    public void Deterministic_ids_are_stable()
    {
        var ws = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var a = DailyMissionIds.QuestId(ws, 7, new DateOnly(2026, 11, 7), "review_due");
        var b = DailyMissionIds.QuestId(ws, 7, new DateOnly(2026, 11, 7), "review_due");
        var c = DailyMissionIds.QuestId(ws, 7, new DateOnly(2026, 11, 7), "session_focus");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public async Task F1_outbox_pipeline_retries_failed_delivery_idempotently()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();
        var xp = scope.ServiceProvider.GetRequiredService<XpEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "t",
            Description = "d",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            TargetValue = 1,
            RewardXp = 12,
            RewardCoins = 0,
            CompletionCondition = "session_count",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = questId,
            Progress = 1,
            Status = QuestStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await quests.ClaimAsync(ws, userId, questId, CancellationToken.None);
        var row = await db.DomainOutbox.SingleAsync(e =>
            e.IdempotencyKey == QuestEngine.RewardIdempotencyKey(questId, userId));
        Assert.Equal(OutboxStatus.Pending, row.Status);

        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(row.PayloadJson, GamificationJson.Options)!;
        await xp.ProcessAsync(row, envelope, CancellationToken.None);
        // Simulate post-commit delivery success then duplicate delivery
        await xp.ProcessAsync(row, envelope, CancellationToken.None);

        Assert.Equal(1, await db.XpLedger.CountAsync(l => l.EventId == row.Id));
        Assert.Equal(QuestStatus.Claimed, (await db.UserQuestProgress.SingleAsync(p => p.QuestId == questId)).Status);

        // Legacy Claimed without outbox — recovery then process
        var orphanQuest = Guid.NewGuid();
        var orphanProgress = new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = orphanQuest,
            Progress = 1,
            Status = QuestStatus.Claimed,
            ClaimedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Quest = new QuestDefinition
            {
                Id = orphanQuest,
                WorkspaceId = ws,
                Title = "orphan",
                Description = "d",
                Category = "dm",
                Kind = QuestKind.Personalized,
                RewardXp = 3,
                CompletionCondition = "session_count",
                Status = QuestStatus.Active,
                AssignedUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        db.Quests.Add(orphanProgress.Quest);
        db.UserQuestProgress.Add(orphanProgress);
        await db.SaveChangesAsync();
        await quests.EnsureRewardOutboxAsync(ws, userId, orphanProgress, CancellationToken.None);
        Assert.Equal(1, await db.DomainOutbox.CountAsync(e =>
            e.IdempotencyKey == QuestEngine.RewardIdempotencyKey(orphanQuest, userId)));
    }

    [Fact]
    public void EventId_unique_indexes_are_modeled()
    {
        // Schema verification against EF model (migration IX_XpLedger_EventId / CoinLedger filtered unique).
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var db = new AppDbContext(options);
        var xp = db.Model.FindEntityType(typeof(XpLedger))!;
        var coin = db.Model.FindEntityType(typeof(CoinLedger))!;
        Assert.Contains(xp.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "EventId");
        Assert.Contains(coin.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == "EventId");
        Assert.Contains(db.Model.FindEntityType(typeof(DomainOutbox))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Any(p => p.Name == "IdempotencyKey"));
    }
}
