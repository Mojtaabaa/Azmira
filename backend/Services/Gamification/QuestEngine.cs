using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class QuestEngine(AppDbContext db, OutboxPublisher outbox)
{
    public const string ConsumerName = "QuestEngine";

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

        // Expire outdated personal progress
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

        if (progress.Status != QuestStatus.Completed)
        {
            throw new InvalidOperationException("این ماموریت هنوز قابل دریافت پاداش نیست.");
        }

        progress.Status = QuestStatus.Claimed;
        progress.ClaimedAt = DateTime.UtcNow;
        progress.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await outbox.PublishAsync(
            workspaceId,
            userId,
            DomainEventTypes.QuestCompleted,
            $"quest:{questId}:{userId}:completed",
            new
            {
                questId,
                rewardXp = progress.Quest.RewardXp,
                rewardCoins = progress.Quest.RewardCoins,
            },
            cancellationToken);
    }

    private static int ProgressDelta(QuestDefinition quest, DomainEventEnvelope envelope)
    {
        return quest.CompletionCondition switch
        {
            "study_minutes" when envelope.EventType == DomainEventTypes.StudySessionCompleted
                => EventData.ReadInt(envelope.Data, "durationMinutes"),
            "session_count" when envelope.EventType == DomainEventTypes.StudySessionCompleted => 1,
            "assessment_count" when envelope.EventType == DomainEventTypes.AssessmentSubmitted => 1,
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
