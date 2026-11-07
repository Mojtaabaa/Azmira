using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class GamificationQueryService(AppDbContext db, GamificationProfileService profiles)
{
    public async Task<object> GetProfileAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var profile = await profiles.ResolveAsync(workspaceId, cancellationToken);
        var levels = profiles.ParseLevels(profile);
        var stats = await db.UserGameStats.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken)
            ?? new UserGameStats { WorkspaceId = workspaceId, UserId = userId, Level = 1, TimeZone = "Asia/Tehran" };

        var (current, next, progressXp, needXp) = LevelEngine.Progress(stats.XpTotal, levels);
        var badges = await db.UserBadges.AsNoTracking()
            .Include(item => item.Badge)
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .OrderByDescending(item => item.AwardedAt)
            .Select(item => new
            {
                id = item.BadgeId,
                code = item.Badge.Code,
                title = item.Badge.Title,
                description = item.Badge.Description,
                tier = item.Badge.Tier.ToString().ToLowerInvariant(),
                icon = item.Badge.Icon,
                awardedAt = item.AwardedAt,
                hidden = item.Badge.IsHidden,
            })
            .ToListAsync(cancellationToken);

        var activeQuests = await db.UserQuestProgress.AsNoTracking()
            .Include(item => item.Quest)
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Status == QuestStatus.Active)
            .Select(item => MapQuest(item))
            .ToListAsync(cancellationToken);

        var completedQuests = await db.UserQuestProgress.AsNoTracking()
            .Include(item => item.Quest)
            .Where(item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && (item.Status == QuestStatus.Completed || item.Status == QuestStatus.Claimed))
            .OrderByDescending(item => item.CompletedAt)
            .Take(20)
            .Select(item => MapQuest(item))
            .ToListAsync(cancellationToken);

        var recent = await db.GameNotifications.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(item => new
            {
                id = item.Id,
                kind = item.Kind.ToString(),
                title = item.Title,
                body = item.Body,
                read = item.Read,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var calendar = await db.StreakDays.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .OrderByDescending(item => item.LocalDate)
            .Take(60)
            .Select(item => new
            {
                date = item.LocalDate,
                kind = item.Kind.ToString().ToLowerInvariant(),
                minutes = item.Minutes,
            })
            .ToListAsync(cancellationToken);

        return new
        {
            xpTotal = stats.XpTotal,
            coins = stats.Coins,
            level = current.Level,
            levelTitle = current.Title,
            levelIcon = current.Icon,
            nextLevelXp = next?.RequiredTotalXp,
            nextLevelTitle = next?.Title,
            levelProgressXp = progressXp,
            levelNeedXp = needXp,
            levelProgressRatio = needXp == 0 ? 1d : Math.Clamp(progressXp / (double)needXp, 0, 1),
            streakCurrent = stats.StreakCurrent,
            streakBest = stats.StreakBest,
            streakFreezes = stats.StreakFreezes,
            timeZone = stats.TimeZone,
            earnedBadges = badges,
            activeQuests,
            completedQuests,
            recentAchievements = recent,
            streakCalendar = calendar,
            dailyXpCap = profile.DailyXpCap,
        };
    }

    public async Task<object> GetLedgerAsync(Guid workspaceId, int userId, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 200);
        var rows = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => new
            {
                id = item.Id,
                delta = item.Delta,
                balanceAfter = item.BalanceAfter,
                reason = item.Reason,
                ruleCode = item.RuleCode,
                kind = item.Kind.ToString().ToLowerInvariant(),
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items = rows };
    }

    public async Task<object> GetNotificationsAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var items = await db.GameNotifications.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && !item.Read)
            .OrderByDescending(item => item.CreatedAt)
            .Take(30)
            .Select(item => new
            {
                id = item.Id,
                kind = item.Kind.ToString(),
                title = item.Title,
                body = item.Body,
                payload = item.PayloadJson,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task MarkNotificationReadAsync(Guid workspaceId, int userId, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.GameNotifications.FirstOrDefaultAsync(
            item => item.Id == id && item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (row is null) return;
        row.Read = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<object> WeeklyLeaderboardAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
        var entries = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Kind == LedgerKind.Earn && item.CreatedAt >= start)
            .GroupBy(item => item.UserId)
            .Select(group => new { userId = group.Key, xp = group.Sum(item => item.Delta) })
            .OrderByDescending(item => item.xp)
            .Take(50)
            .ToListAsync(cancellationToken);

        var userIds = entries.Select(item => item.userId).ToList();
        var names = await db.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Username, user.DisplayName })
            .ToListAsync(cancellationToken);
        var nameMap = names.ToDictionary(item => item.Id);

        var ranked = entries.Select((item, index) =>
        {
            nameMap.TryGetValue(item.userId, out var user);
            return new
            {
                userId = item.userId,
                displayName = user?.DisplayName ?? user?.Username ?? $"#{item.userId}",
                xp = item.xp,
                rank = index + 1,
            };
        });

        var period = $"{start:yyyy}-W{ISOWeek(start):00}";
        return new { period, entries = ranked };
    }

    private static object MapQuest(UserQuestProgress item) => new
    {
        id = item.QuestId,
        title = item.Quest.Title,
        description = item.Quest.Description,
        category = item.Quest.Category,
        kind = item.Quest.Kind.ToString(),
        progress = item.Progress,
        targetValue = item.Quest.TargetValue,
        rewardXp = item.Quest.RewardXp,
        rewardCoins = item.Quest.RewardCoins,
        status = item.Status.ToString(),
        endsAt = item.Quest.EndsAt,
        difficulty = item.Quest.Difficulty,
    };

    private static int ISOWeek(DateTime date) =>
        System.Globalization.ISOWeek.GetWeekOfYear(date);
}
