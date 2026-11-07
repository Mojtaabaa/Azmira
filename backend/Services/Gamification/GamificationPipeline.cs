using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

/// <summary>Shared processing used by the hosted outbox worker and tests.</summary>
public sealed class GamificationPipeline(
    AppDbContext db,
    XpEngine xp,
    LevelEngine levels,
    StreakEngine streaks,
    BadgeEngine badges,
    QuestEngine quests,
    WebhookService webhooks)
{
    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken, int take = 50)
    {
        var now = DateTime.UtcNow;
        var rows = await db.DomainOutbox
            .Where(item => item.Status == OutboxStatus.Pending
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
            .OrderBy(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var row in rows)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(row.PayloadJson, GamificationJson.Options)
                    ?? throw new InvalidOperationException("payload نامعتبر");

                await xp.ProcessAsync(row, envelope, cancellationToken);
                await levels.ProcessAsync(row, envelope, cancellationToken);
                await streaks.ProcessAsync(row, envelope, cancellationToken);
                await badges.ProcessAsync(row, envelope, cancellationToken);
                await quests.ProcessAsync(row, envelope, cancellationToken);

                await webhooks.EnqueueForEventAsync(
                    row.WorkspaceId,
                    row.Id,
                    envelope.EventType,
                    envelope.Data,
                    cancellationToken);

                row.Status = OutboxStatus.Processed;
                row.ProcessedAt = DateTime.UtcNow;
                row.LastError = null;
                await db.SaveChangesAsync(cancellationToken);
                processed++;
            }
            catch (Exception error)
            {
                row.Attempts += 1;
                row.LastError = error.Message.Length > 1000 ? error.Message[..1000] : error.Message;
                if (row.Attempts >= 10) row.Status = OutboxStatus.Failed;
                else
                {
                    row.Status = OutboxStatus.Pending;
                    row.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, Math.Min(row.Attempts, 6)));
                }

                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return processed;
    }
}
