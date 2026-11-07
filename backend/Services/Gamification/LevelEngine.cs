using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class LevelEngine(AppDbContext db, GamificationProfileService profiles, WebhookService webhooks)
{
    public const string ConsumerName = "LevelEngine";

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

        var stats = await db.UserGameStats.FirstOrDefaultAsync(
            item => item.WorkspaceId == row.WorkspaceId && item.UserId == userId,
            cancellationToken);
        if (stats is null)
        {
            await Mark(row.Id, cancellationToken);
            return;
        }

        var profile = await profiles.ResolveAsync(row.WorkspaceId, cancellationToken);
        var levels = profiles.ParseLevels(profile).OrderBy(item => item.RequiredTotalXp).ToList();
        if (levels.Count == 0) levels = DefaultGamificationSeed.DefaultLevels();

        var previous = stats.Level;
        var nextLevel = levels[0];
        foreach (var level in levels)
        {
            if (stats.XpTotal >= level.RequiredTotalXp) nextLevel = level;
        }

        if (nextLevel.Level > previous)
        {
            stats.Level = nextLevel.Level;
            stats.UpdatedAt = DateTime.UtcNow;
            var payload = new
            {
                level = nextLevel.Level,
                title = nextLevel.Title,
                icon = nextLevel.Icon,
                previous,
                userId,
            };
            db.GameNotifications.Add(new GameNotification
            {
                Id = Guid.NewGuid(),
                WorkspaceId = row.WorkspaceId,
                UserId = userId,
                Kind = GameNotificationKind.LevelUp,
                Title = "سطح جدید!",
                Body = $"به سطح {nextLevel.Level} — {nextLevel.Title} رسیدی.",
                PayloadJson = JsonSerializer.Serialize(payload, GamificationJson.Options),
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
            await webhooks.EnqueueForEventAsync(row.WorkspaceId, row.Id, WebhookEventTypes.LevelUp, payload, cancellationToken);
        }

        await Mark(row.Id, cancellationToken);
    }

    public static (LevelDefinition Current, LevelDefinition? Next, int ProgressXp, int NeedXp) Progress(
        int xpTotal,
        IReadOnlyList<LevelDefinition> levels)
    {
        var ordered = levels.OrderBy(item => item.RequiredTotalXp).ToList();
        if (ordered.Count == 0) ordered = DefaultGamificationSeed.DefaultLevels();
        var current = ordered[0];
        LevelDefinition? next = null;
        for (var i = 0; i < ordered.Count; i++)
        {
            if (xpTotal >= ordered[i].RequiredTotalXp) current = ordered[i];
            if (ordered[i].RequiredTotalXp > xpTotal)
            {
                next = ordered[i];
                break;
            }
        }

        if (next is null) return (current, null, 0, 0);
        var progress = Math.Max(0, xpTotal - current.RequiredTotalXp);
        var need = Math.Max(1, next.RequiredTotalXp - current.RequiredTotalXp);
        return (current, next, progress, need);
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
