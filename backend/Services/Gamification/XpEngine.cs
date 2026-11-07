using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class XpEngine(AppDbContext db, GamificationProfileService profiles)
{
    public const string ConsumerName = "XpEngine";

    public async Task ProcessAsync(DomainOutbox row, DomainEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await AlreadyProcessed(row.Id, cancellationToken)) return;
        if (envelope.ActorUserId is not int userId) 
        {
            await MarkProcessed(row.Id, cancellationToken);
            return;
        }

        var profile = await profiles.ResolveAsync(row.WorkspaceId, cancellationToken);
        var rules = profiles.ParseRules(profile);
        if (rules.DisabledEvents.Contains(envelope.EventType, StringComparer.OrdinalIgnoreCase))
        {
            await MarkProcessed(row.Id, cancellationToken);
            return;
        }

        var awards = rules.Awards.Where(rule => string.Equals(rule.On, envelope.EventType, StringComparison.OrdinalIgnoreCase)).ToList();
        if (awards.Count == 0)
        {
            // QuestCompleted may carry rewardXp in payload
            if (envelope.EventType == DomainEventTypes.QuestCompleted)
            {
                var questXp = EventData.ReadInt(envelope.Data, "rewardXp");
                var questCoins = EventData.ReadInt(envelope.Data, "rewardCoins");
                if (questXp > 0 || questCoins > 0)
                {
                    await ApplyAsync(row.WorkspaceId, userId, row.Id, questXp, questCoins, "quest_reward", "quest", profile.DailyXpCap, cancellationToken);
                }
            }

            await MarkProcessed(row.Id, cancellationToken);
            return;
        }

        foreach (var rule in awards)
        {
            var xp = ComputeXp(rule, envelope);
            if (xp <= 0 && (rule.Coins ?? 0) <= 0) continue;
            if (rule.MinScore is double minScore)
            {
                var score = EventData.ReadDouble(envelope.Data, "score");
                if (score < minScore) continue;
            }

            await ApplyAsync(
                row.WorkspaceId,
                userId,
                row.Id,
                xp,
                rule.Coins ?? 0,
                rule.Code,
                rule.Code,
                Math.Min(profile.DailyXpCap, rule.MaxPerDay ?? profile.DailyXpCap),
                cancellationToken);
        }

        await MarkProcessed(row.Id, cancellationToken);
    }

    public async Task<(int Balance, Guid LedgerId)> AdjustAsync(
        Guid workspaceId,
        int userId,
        int delta,
        string reason,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var stats = await GetOrCreateStatsAsync(workspaceId, userId, cancellationToken);
        stats.XpTotal = Math.Max(0, stats.XpTotal + delta);
        stats.UpdatedAt = DateTime.UtcNow;
        var ledger = new XpLedger
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            EventId = null,
            Delta = delta,
            BalanceAfter = stats.XpTotal,
            Reason = reason,
            RuleCode = "admin_adjust",
            Kind = LedgerKind.Adjust,
            CreatedAt = DateTime.UtcNow,
        };
        db.XpLedger.Add(ledger);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ActorUserId = actorUserId,
            Action = "xp.adjust",
            TargetType = "UserGameStats",
            TargetId = $"{workspaceId}:{userId}",
            DetailJson = JsonSerializer.Serialize(new { delta, reason, balanceAfter = stats.XpTotal }, GamificationJson.Options),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (stats.XpTotal, ledger.Id);
    }

    private async Task ApplyAsync(
        Guid workspaceId,
        int userId,
        Guid eventId,
        int xp,
        int coins,
        string reason,
        string ruleCode,
        int dailyCap,
        CancellationToken cancellationToken)
    {
        if (await db.XpLedger.AnyAsync(item => item.EventId == eventId && item.RuleCode == ruleCode, cancellationToken))
        {
            return;
        }

        var stats = await GetOrCreateStatsAsync(workspaceId, userId, cancellationToken);
        var awardedToday = await db.XpLedger
            .Where(item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.Kind == LedgerKind.Earn
                && item.CreatedAt >= DateTime.UtcNow.Date)
            .SumAsync(item => (int?)item.Delta, cancellationToken) ?? 0;

        var remaining = Math.Max(0, dailyCap - awardedToday);
        var grantXp = Math.Min(Math.Max(0, xp), remaining);
        if (grantXp > 0)
        {
            stats.XpTotal += grantXp;
            db.XpLedger.Add(new XpLedger
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                EventId = eventId,
                Delta = grantXp,
                BalanceAfter = stats.XpTotal,
                Reason = reason,
                RuleCode = ruleCode,
                Kind = LedgerKind.Earn,
                CreatedAt = DateTime.UtcNow,
            });
            db.GameNotifications.Add(new GameNotification
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Kind = GameNotificationKind.XpGained,
                Title = "امتیاز جدید",
                Body = $"+{grantXp} XP",
                PayloadJson = JsonSerializer.Serialize(new { xp = grantXp, ruleCode }, GamificationJson.Options),
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (coins > 0)
        {
            if (!await db.CoinLedger.AnyAsync(item => item.EventId == eventId && item.RuleCode == ruleCode, cancellationToken))
            {
                stats.Coins += coins;
                db.CoinLedger.Add(new CoinLedger
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    EventId = eventId,
                    Delta = coins,
                    BalanceAfter = stats.Coins,
                    Reason = reason,
                    RuleCode = ruleCode,
                    Kind = LedgerKind.Earn,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        stats.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static int ComputeXp(XpAwardRule rule, DomainEventEnvelope envelope)
    {
        if (rule.PerMinutes is int perMinutes and > 0)
        {
            var minutes = EventData.ReadInt(envelope.Data, "durationMinutes");
            if (minutes <= 0) return 0;
            return (minutes / perMinutes) * rule.Xp;
        }

        if (envelope.EventType == DomainEventTypes.QuestCompleted)
        {
            var reward = EventData.ReadInt(envelope.Data, "rewardXp");
            return reward > 0 ? reward : rule.Xp;
        }

        return rule.Xp;
    }

    private async Task<UserGameStats> GetOrCreateStatsAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var stats = await db.UserGameStats.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (stats is not null) return stats;

        var tz = await profiles.ResolveTimeZoneAsync(workspaceId, cancellationToken);
        stats = new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Level = 1,
            StreakFreezes = DefaultGamificationSeed.DefaultStreak().StartingFreezes,
            TimeZone = tz,
            UpdatedAt = DateTime.UtcNow,
        };
        db.UserGameStats.Add(stats);
        await db.SaveChangesAsync(cancellationToken);
        return stats;
    }

    private async Task<bool> AlreadyProcessed(Guid eventId, CancellationToken cancellationToken) =>
        await db.ProcessedEvents.AnyAsync(item => item.ConsumerName == ConsumerName && item.EventId == eventId, cancellationToken);

    private async Task MarkProcessed(Guid eventId, CancellationToken cancellationToken)
    {
        if (await AlreadyProcessed(eventId, cancellationToken)) return;
        db.ProcessedEvents.Add(new ProcessedEvent
        {
            ConsumerName = ConsumerName,
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

}
