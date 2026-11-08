using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class DailyMissionTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateAsync(string? tz = "Asia/Tehran")
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<DailyMissionService>();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "missioner",
            NormalizedUsername = "missioner",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            PlatformRole = 0,
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
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Data = "{}",
            LocaleDefault = "fa-IR",
            TimeZone = tz ?? "Asia/Tehran",
            FeatureFlags = """{"gamification":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        return (sp, workspaceId, user.Id);
    }

    [Fact]
    public async Task Assigns_at_most_three_missions_per_local_day()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
        var first = JsonDocument.Parse(JsonSerializer.Serialize(await missions.GetTodayAsync(ws, userId, CancellationToken.None)));
        var second = JsonDocument.Parse(JsonSerializer.Serialize(await missions.GetTodayAsync(ws, userId, CancellationToken.None)));
        var n1 = first.RootElement.GetProperty("items").GetArrayLength();
        var n2 = second.RootElement.GetProperty("items").GetArrayLength();
        Assert.True(n1 <= DailyMissionCatalog.MaxMissionsPerDay);
        Assert.Equal(n1, n2);
        Assert.True(n1 > 0);
    }

    [Fact]
    public async Task Does_not_duplicate_assignment_same_day()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        var count1 = await db.Quests.CountAsync(q => q.WorkspaceId == ws && q.Category == DailyMissionCatalog.Category);
        await missions.GetTodayAsync(ws, userId, CancellationToken.None);
        var count2 = await db.Quests.CountAsync(q => q.WorkspaceId == ws && q.Category == DailyMissionCatalog.Category);
        Assert.Equal(count1, count2);
    }

    [Fact]
    public async Task Manual_condition_never_progresses_from_events()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();

        var quest = new QuestDefinition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            Title = "دستی",
            Description = "manual",
            Category = "classroom",
            Kind = QuestKind.TeacherAssigned,
            TargetValue = 1,
            RewardXp = 10,
            RewardCoins = 0,
            CompletionCondition = "manual",
            Status = QuestStatus.Active,
            AssignedUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Quests.Add(quest);
        db.UserQuestProgress.Add(new UserQuestProgress
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            QuestId = quest.Id,
            Progress = 0,
            Status = QuestStatus.Active,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var eventId = await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, $"r-{Guid.NewGuid():N}", new { cardId = Guid.NewGuid() }, CancellationToken.None);
        var row = await db.DomainOutbox.FirstAsync(e => e.Id == eventId);
        using var payload = JsonDocument.Parse(row.PayloadJson);
        var envelope = new DomainEventEnvelope
        {
            EventId = row.Id,
            EventType = row.EventType,
            EventVersion = row.EventVersion,
            OccurredAt = row.OccurredAt,
            WorkspaceId = row.WorkspaceId,
            ActorUserId = row.UserId,
            IdempotencyKey = row.IdempotencyKey,
            Data = payload.RootElement.GetProperty("data").Clone(),
        };
        await quests.ProcessAsync(row, envelope, CancellationToken.None);

        var progress = await db.UserQuestProgress.FirstAsync(p => p.QuestId == quest.Id);
        Assert.Equal(0, progress.Progress);
        Assert.Equal(QuestStatus.Active, progress.Status);
    }

    [Fact]
    public async Task Teacher_confirm_manual_is_idempotent()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "تایید معلم",
            Description = "x",
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

        await quests.ConfirmManualAsync(ws, userId, questId, teacherUserId: 99, note: "ok", CancellationToken.None);
        await quests.ConfirmManualAsync(ws, userId, questId, teacherUserId: 99, note: "again", CancellationToken.None);
        var progress = await db.UserQuestProgress.FirstAsync(p => p.QuestId == questId);
        Assert.Equal(QuestStatus.Completed, progress.Status);
        Assert.Equal(1, progress.Progress);
    }

    [Fact]
    public async Task Claim_is_idempotent_and_publishes_once()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quests = scope.ServiceProvider.GetRequiredService<QuestEngine>();

        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "قابل دریافت",
            Description = "x",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            TargetValue = 1,
            RewardXp = 12,
            RewardCoins = 3,
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

        var published = await db.DomainOutbox.CountAsync(e =>
            e.WorkspaceId == ws && e.EventType == DomainEventTypes.QuestCompleted && e.IdempotencyKey == $"quest:{questId}:{userId}:completed");
        Assert.Equal(1, published);
        var progress = await db.UserQuestProgress.FirstAsync(p => p.QuestId == questId);
        Assert.Equal(QuestStatus.Claimed, progress.Status);
    }

    [Fact]
    public void MissionClock_local_day_bounds_are_contiguous()
    {
        var date = new DateOnly(2026, 3, 21);
        var (start, end) = MissionClock.LocalDayUtcBounds(date, "Asia/Tehran");
        var (startNext, _) = MissionClock.LocalDayUtcBounds(date.AddDays(1), "Asia/Tehran");
        Assert.True(end < startNext);
        Assert.Equal(date, MissionClock.ToLocalDate(start.AddMinutes(1), "Asia/Tehran"));
    }

    [Fact]
    public void Timezone_change_does_not_rewrite_meta_date_parser()
    {
        var date = new DateOnly(2026, 11, 7);
        var meta = DailyMissionCatalog.MetaPrefix("review_due", date);
        Assert.True(DailyMissionCatalog.TryParseMeta($"{meta} متن", out var tid, out var parsed));
        Assert.Equal("review_due", tid);
        Assert.Equal(date, parsed);
    }

    [Fact]
    public async Task Evidence_sync_completes_review_mission()
    {
        var (sp, ws, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var missions = scope.ServiceProvider.GetRequiredService<DailyMissionService>();
        var outbox = scope.ServiceProvider.GetRequiredService<OutboxPublisher>();

        var tz = "Asia/Tehran";
        var local = MissionClock.ToLocalDate(DateTime.UtcNow, tz);
        var (start, end) = MissionClock.LocalDayUtcBounds(local, tz);
        var questId = Guid.NewGuid();
        db.Quests.Add(new QuestDefinition
        {
            Id = questId,
            WorkspaceId = ws,
            Title = "بازبینی",
            Description = $"{DailyMissionCatalog.MetaPrefix("review_due", local)} بازبینی",
            Category = DailyMissionCatalog.Category,
            Kind = QuestKind.Personalized,
            StartsAt = start,
            EndsAt = end,
            TargetValue = 1,
            RewardXp = 10,
            CompletionCondition = "review_count",
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
            Progress = 0,
            Status = QuestStatus.Active,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await outbox.PublishAsync(ws, userId, DomainEventTypes.ReviewCompleted, $"rev-{Guid.NewGuid():N}", new { ok = true }, CancellationToken.None);
        await missions.SyncAsync(ws, userId, CancellationToken.None);

        var progress = await db.UserQuestProgress.FirstAsync(p => p.QuestId == questId);
        Assert.Equal(QuestStatus.Completed, progress.Status);
        Assert.Equal(1, progress.Progress);
    }
}
