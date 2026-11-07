using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class BadgeEngine(AppDbContext db, GamificationProfileService profiles, WebhookService webhooks)
{
    public const string ConsumerName = "BadgeEngine";

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

        var profile = await profiles.ResolveAsync(row.WorkspaceId, cancellationToken);
        var badges = await db.Badges.AsNoTracking()
            .Where(item => item.ProfileId == profile.Id || item.WorkspaceId == row.WorkspaceId)
            .ToListAsync(cancellationToken);

        var owned = await db.UserBadges.AsNoTracking()
            .Where(item => item.WorkspaceId == row.WorkspaceId && item.UserId == userId)
            .Select(item => item.BadgeId)
            .ToListAsync(cancellationToken);
        var ownedSet = owned.ToHashSet();

        var stats = await db.UserGameStats.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == row.WorkspaceId && item.UserId == userId, cancellationToken);

        foreach (var badge in badges)
        {
            if (!badge.AllowRepeat && ownedSet.Contains(badge.Id)) continue;
            if (!await MeetsAsync(badge, row.WorkspaceId, userId, envelope, stats, cancellationToken)) continue;

            db.UserBadges.Add(new UserBadge
            {
                Id = Guid.NewGuid(),
                WorkspaceId = row.WorkspaceId,
                UserId = userId,
                BadgeId = badge.Id,
                EventId = row.Id,
                AwardedAt = DateTime.UtcNow,
            });
            ownedSet.Add(badge.Id);
            var payload = new
            {
                badgeId = badge.Id,
                code = badge.Code,
                tier = badge.Tier.ToString().ToLowerInvariant(),
                icon = badge.Icon,
                userId,
            };
            db.GameNotifications.Add(new GameNotification
            {
                Id = Guid.NewGuid(),
                WorkspaceId = row.WorkspaceId,
                UserId = userId,
                Kind = GameNotificationKind.BadgeUnlocked,
                Title = badge.IsHidden ? "نشان مخفی!" : "نشان جدید",
                Body = badge.Title,
                PayloadJson = JsonSerializer.Serialize(payload, GamificationJson.Options),
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
            await webhooks.EnqueueForEventAsync(row.WorkspaceId, row.Id, WebhookEventTypes.BadgeAwarded, payload, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await Mark(row.Id, cancellationToken);
    }

    private async Task<bool> MeetsAsync(
        BadgeDefinition badge,
        Guid workspaceId,
        int userId,
        DomainEventEnvelope envelope,
        UserGameStats? stats,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(badge.ConditionsJson) ? "{}" : badge.ConditionsJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("streakDays", out var streakDays) && streakDays.TryGetInt32(out var needStreak))
        {
            return (stats?.StreakCurrent ?? 0) >= needStreak;
        }

        if (root.TryGetProperty("bestStreak", out var best) && best.TryGetInt32(out var needBest))
        {
            return (stats?.StreakBest ?? 0) >= needBest;
        }

        if (root.TryGetProperty("totalStudyMinutes", out var minutesEl) && minutesEl.TryGetInt32(out var needMinutes))
        {
            var total = await SumSessionMinutesAsync(workspaceId, userId, cancellationToken);
            return total >= needMinutes;
        }

        if (root.TryGetProperty("on", out var onEl) && onEl.ValueKind == JsonValueKind.String)
        {
            var on = onEl.GetString() ?? "";
            if (!string.Equals(on, envelope.EventType, StringComparison.OrdinalIgnoreCase)) return false;
            var minCount = root.TryGetProperty("minCount", out var mc) && mc.TryGetInt32(out var n) ? n : 1;
            if (minCount <= 1) return true;
            var count = await db.DomainOutbox.CountAsync(
                item => item.WorkspaceId == workspaceId
                    && item.UserId == userId
                    && item.EventType == on
                    && item.Status != OutboxStatus.Failed,
                cancellationToken);
            return count >= minCount;
        }

        if (root.TryGetProperty("subject", out var subjectEl) || root.TryGetProperty("subjects", out _))
        {
            var minScore = root.TryGetProperty("minScore", out var scoreEl) && scoreEl.TryGetDouble(out var s) ? s : 80;
            var minCount = root.TryGetProperty("minCount", out var countEl) && countEl.TryGetInt32(out var c) ? c : 1;
            var subjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("subject", out var one) && one.ValueKind == JsonValueKind.String)
            {
                subjects.Add(one.GetString() ?? "");
            }

            if (root.TryGetProperty("subjects", out var many) && many.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in many.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) subjects.Add(item.GetString() ?? "");
                }
            }

            var tests = await db.StudyRecords.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Collection == "tests")
                .Select(item => item.Data)
                .ToListAsync(cancellationToken);
            var matched = 0;
            foreach (var raw in tests)
            {
                using var testDoc = JsonDocument.Parse(raw);
                var subject = testDoc.RootElement.TryGetProperty("subject", out var sub) && sub.ValueKind == JsonValueKind.String
                    ? sub.GetString() ?? ""
                    : "";
                if (!subjects.Contains(subject)) continue;
                var score = testDoc.RootElement.TryGetProperty("score", out var sc) && sc.TryGetDouble(out var scoreValue)
                    ? scoreValue
                    : 0;
                if (score >= minScore) matched++;
            }

            return matched >= minCount;
        }

        if (root.TryGetProperty("dailyGoalsInRow", out var goalsEl) && goalsEl.TryGetInt32(out var needGoals))
        {
            var goals = await db.DomainOutbox.CountAsync(
                item => item.WorkspaceId == workspaceId
                    && item.UserId == userId
                    && item.EventType == DomainEventTypes.StudyGoalCompleted
                    && item.Status == OutboxStatus.Processed,
                cancellationToken);
            return goals >= needGoals;
        }

        return false;
    }

    private async Task<int> SumSessionMinutesAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var rows = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Collection == "sessions")
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);
        var total = 0;
        foreach (var raw in rows)
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("durationMinutes", out var m) && m.TryGetInt32(out var minutes))
            {
                total += minutes;
            }
        }

        return total;
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
