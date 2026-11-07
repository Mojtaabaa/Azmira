using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class QuestEngine(AppDbContext db, OutboxPublisher outbox)
{
    public const string ConsumerName = "QuestEngine";
    /// <summary>Unclaimed Completed missions may be claimed until EndsAt + this grace.</summary>
    public static readonly TimeSpan ClaimGraceAfterEndsAt = TimeSpan.FromHours(24);

    /// <summary>Process-local claim serialization (complements SQL conditional update / unique outbox key).</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> ClaimGates = new();

    public async Task ProcessAsync(DomainOutbox row, DomainEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await db.ProcessedEvents.AnyAsync(item => item.ConsumerName == ConsumerName && item.EventId == row.Id, cancellationToken))
        {
            return;
        }

        if (envelope.ActorUserId is not int userId)
        {
            await Mark(row.Id, cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        var quests = await db.Quests
            .Where(item => item.Status == QuestStatus.Active
                && (item.WorkspaceId == null || item.WorkspaceId == row.WorkspaceId)
                && (item.AssignedUserId == null || item.AssignedUserId == userId)
                && (item.StartsAt == null || item.StartsAt <= now)
                && (item.EndsAt == null || item.EndsAt >= now))
            .ToListAsync(cancellationToken);

        foreach (var quest in quests)
        {
            var increment = ProgressDelta(quest, envelope);
            if (increment <= 0) continue;

            var progress = await db.UserQuestProgress.FirstOrDefaultAsync(
                item => item.WorkspaceId == row.WorkspaceId && item.UserId == userId && item.QuestId == quest.Id,
                cancellationToken);
            if (progress is null)
            {
                progress = new UserQuestProgress
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = row.WorkspaceId,
                    UserId = userId,
                    QuestId = quest.Id,
                    Progress = 0,
                    Status = QuestStatus.Active,
                    UpdatedAt = now,
                };
                db.UserQuestProgress.Add(progress);
            }

            if (progress.Status is QuestStatus.Completed or QuestStatus.Claimed or QuestStatus.Expired) continue;

            progress.Progress = Math.Min(quest.TargetValue, progress.Progress + increment);
            progress.UpdatedAt = now;
            if (progress.Progress >= quest.TargetValue)
            {
                progress.Status = QuestStatus.Completed;
                progress.CompletedAt = now;
                db.GameNotifications.Add(new GameNotification
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = row.WorkspaceId,
                    UserId = userId,
                    Kind = GameNotificationKind.QuestCompleted,
                    Title = "ماموریت تمام شد",
                    Body = quest.Title,
                    PayloadJson = JsonSerializer.Serialize(new { questId = quest.Id }, GamificationJson.Options),
                    CreatedAt = now,
                });
            }
        }

        var expired = await db.UserQuestProgress
            .Include(item => item.Quest)
            .Where(item => item.WorkspaceId == row.WorkspaceId
                && item.UserId == userId
                && item.Status == QuestStatus.Active
                && item.Quest.EndsAt != null
                && item.Quest.EndsAt < now)
            .ToListAsync(cancellationToken);
        foreach (var item in expired)
        {
            item.Status = QuestStatus.Expired;
            item.UpdatedAt = now;
        }

        // Expire Completed past grace (no reward)
        var graceCutoff = now - ClaimGraceAfterEndsAt;
        var staleCompleted = await db.UserQuestProgress
            .Include(item => item.Quest)
            .Where(item => item.WorkspaceId == row.WorkspaceId
                && item.UserId == userId
                && item.Status == QuestStatus.Completed
                && item.Quest.EndsAt != null
                && item.Quest.EndsAt < graceCutoff)
            .ToListAsync(cancellationToken);
        foreach (var item in staleCompleted)
        {
            item.Status = QuestStatus.Expired;
            item.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await Mark(row.Id, cancellationToken);
    }

    public async Task ClaimAsync(Guid workspaceId, int userId, Guid questId, CancellationToken cancellationToken)
    {
        var progress = await db.UserQuestProgress
            .Include(item => item.Quest)
            .FirstOrDefaultAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == userId && item.QuestId == questId,
                cancellationToken)
            ?? throw new InvalidOperationException("پیشرفت ماموریت پیدا نشد.");

        if (progress.Status == QuestStatus.Claimed)
        {
            await EnsureRewardOutboxAsync(workspaceId, userId, progress, cancellationToken);
            return;
        }

        if (progress.Status == QuestStatus.Expired)
        {
            throw new InvalidOperationException("این ماموریت منقضی شده و قابل دریافت نیست.");
        }

        if (progress.Status != QuestStatus.Completed)
        {
            throw new InvalidOperationException("این ماموریت هنوز قابل دریافت پاداش نیست.");
        }

        var now = DateTime.UtcNow;
        if (progress.Quest.EndsAt is DateTime endsAt && now > endsAt + ClaimGraceAfterEndsAt)
        {
            progress.Status = QuestStatus.Expired;
            progress.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("مهلت دریافت پاداش تمام شده است.");
        }

        var idempotencyKey = RewardIdempotencyKey(questId, userId);
        var rewardPayload = new
        {
            questId,
            rewardXp = progress.Quest.RewardXp,
            rewardCoins = progress.Quest.RewardCoins,
        };

        var gate = ClaimGates.GetOrAdd(progress.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await SqlConcurrency.RunWithClearAsync(db, async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    // Conditional transition: only one concurrent winner
                    int affected;
                    if (db.Database.IsRelational())
                    {
                        affected = await db.UserQuestProgress
                            .Where(item => item.Id == progress.Id && item.Status == QuestStatus.Completed)
                            .ExecuteUpdateAsync(setters => setters
                                .SetProperty(item => item.Status, QuestStatus.Claimed)
                                .SetProperty(item => item.ClaimedAt, now)
                                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
                    }
                    else
                    {
                        // EF InMemory does not support ExecuteUpdate — reload and transition under the gate.
                        db.ChangeTracker.Clear();
                        var row = await db.UserQuestProgress
                            .FirstOrDefaultAsync(
                                item => item.Id == progress.Id && item.Status == QuestStatus.Completed,
                                cancellationToken);
                        if (row is null)
                        {
                            affected = 0;
                        }
                        else
                        {
                            row.Status = QuestStatus.Claimed;
                            row.ClaimedAt = now;
                            row.UpdatedAt = now;
                            affected = 1;
                        }
                    }

                    if (affected == 0)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        db.ChangeTracker.Clear();
                        var again = await db.UserQuestProgress.AsNoTracking()
                            .FirstAsync(item => item.Id == progress.Id, cancellationToken);
                        if (again.Status == QuestStatus.Claimed)
                        {
                            await EnsureRewardOutboxAsync(workspaceId, userId, progress, cancellationToken);
                            return;
                        }

                        throw new InvalidOperationException("این ماموریت دیگر قابل دریافت نیست.");
                    }

                    await outbox.EnqueueAsync(
                        workspaceId,
                        userId,
                        DomainEventTypes.QuestCompleted,
                        idempotencyKey,
                        rewardPayload,
                        cancellationToken);

                    await db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await tx.RollbackAsync(cancellationToken);
                    throw;
                }
            }, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Recovery: Claimed progress must always have a durable QuestCompleted outbox row.
    /// </summary>
    public async Task EnsureRewardOutboxAsync(
        Guid workspaceId,
        int userId,
        UserQuestProgress progress,
        CancellationToken cancellationToken)
    {
        var key = RewardIdempotencyKey(progress.QuestId, userId);
        var exists = await db.DomainOutbox.AsNoTracking().AnyAsync(
            e => e.WorkspaceId == workspaceId
                && e.EventType == DomainEventTypes.QuestCompleted
                && e.IdempotencyKey == key,
            cancellationToken);
        if (exists) return;

        var quest = progress.Quest ?? await db.Quests.AsNoTracking()
            .FirstAsync(q => q.Id == progress.QuestId, cancellationToken);

        await outbox.PublishAsync(
            workspaceId,
            userId,
            DomainEventTypes.QuestCompleted,
            key,
            new
            {
                questId = progress.QuestId,
                rewardXp = quest.RewardXp,
                rewardCoins = quest.RewardCoins,
                recovery = true,
            },
            cancellationToken);
    }

    public async Task ConfirmManualAsync(
        Guid workspaceId,
        int learnerUserId,
        Guid questId,
        int teacherUserId,
        string? note,
        CancellationToken cancellationToken)
    {
        var quest = await db.Quests.FirstOrDefaultAsync(
            q => q.Id == questId && q.WorkspaceId == workspaceId,
            cancellationToken)
            ?? throw new InvalidOperationException("ماموریت پیدا نشد.");

        if (!string.Equals(quest.CompletionCondition, "manual", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("این ماموریت دستی نیست و باید با شواهد سیستمی تمام شود.");
        }

        if (quest.AssignedUserId is int assigned && assigned != learnerUserId)
        {
            throw new InvalidOperationException("این ماموریت برای این یادگیرنده نیست.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var progress = await db.UserQuestProgress.FirstOrDefaultAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == learnerUserId && item.QuestId == questId,
                cancellationToken);

            if (progress is null)
            {
                progress = new UserQuestProgress
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = learnerUserId,
                    QuestId = questId,
                    Progress = 0,
                    Status = QuestStatus.Active,
                    UpdatedAt = DateTime.UtcNow,
                };
                db.UserQuestProgress.Add(progress);
            }

            var alreadyDone = progress.Status is QuestStatus.Completed or QuestStatus.Claimed;

            if (!alreadyDone)
            {
                progress.Progress = quest.TargetValue;
                progress.Status = QuestStatus.Completed;
                progress.CompletedAt = DateTime.UtcNow;
                progress.UpdatedAt = DateTime.UtcNow;
                db.GameNotifications.Add(new GameNotification
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = learnerUserId,
                    Kind = GameNotificationKind.QuestCompleted,
                    Title = "تایید معلم",
                    Body = string.IsNullOrWhiteSpace(note) ? quest.Title : $"{quest.Title} — {note.Trim()}",
                    PayloadJson = JsonSerializer.Serialize(
                        new { questId, teacherUserId, manual = true },
                        GamificationJson.Options),
                    CreatedAt = DateTime.UtcNow,
                });
            }

            // Durable audit on every confirm call (idempotent duplicate still recorded once via detail key? once per call is OK for audit of attempts)
            // Prefer single audit for first successful confirm; skip if already done to avoid spam
            if (!alreadyDone)
            {
                var noteHash = string.IsNullOrWhiteSpace(note)
                    ? null
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(note.Trim())));
                db.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    ActorUserId = teacherUserId,
                    Action = "quest.manual_confirm",
                    TargetType = "QuestDefinition",
                    TargetId = questId.ToString("N"),
                    DetailJson = JsonSerializer.Serialize(new
                    {
                        studentUserId = learnerUserId,
                        questId,
                        teacherUserId,
                        noteSha256 = noteHash,
                    }, GamificationJson.Options),
                    CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public static string RewardIdempotencyKey(Guid questId, int userId) =>
        $"quest:{questId}:{userId}:completed";

    private static int ProgressDelta(QuestDefinition quest, DomainEventEnvelope envelope)
    {
        if (string.Equals(quest.CompletionCondition, "manual", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return quest.CompletionCondition switch
        {
            "study_minutes" when envelope.EventType == DomainEventTypes.StudySessionCompleted
                => EventData.ReadInt(envelope.Data, "durationMinutes"),
            "session_count" when envelope.EventType == DomainEventTypes.StudySessionCompleted => 1,
            "assessment_count" when envelope.EventType == DomainEventTypes.AssessmentSubmitted => 1,
            "assessment_passed" when envelope.EventType == DomainEventTypes.AssessmentPassed => 1,
            "mastery_improved" when envelope.EventType == DomainEventTypes.MasteryImproved => 1,
            "review_count" when envelope.EventType == DomainEventTypes.ReviewCompleted => 1,
            "daily_goal" when envelope.EventType == DomainEventTypes.StudyGoalCompleted => 1,
            _ => 0,
        };
    }

    private async Task Mark(Guid eventId, CancellationToken cancellationToken)
    {
        if (await db.ProcessedEvents.AnyAsync(item => item.ConsumerName == ConsumerName && item.EventId == eventId, cancellationToken))
        {
            return;
        }

        db.ProcessedEvents.Add(new ProcessedEvent { ConsumerName = ConsumerName, EventId = eventId, ProcessedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }
}
