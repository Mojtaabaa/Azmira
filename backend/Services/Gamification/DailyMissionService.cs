using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class DailyMissionService(
    AppDbContext db,
    GamificationProfileService profiles)
{
    /// <summary>
    /// Conditions that count as time/activity-based; at most one such template per day.
    /// </summary>
    public static readonly HashSet<string> TimeBasedConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "study_minutes",
        "session_count",
    };

    /// <summary>
    /// Legacy missions without StartsAt/EndsAt: measure evidence using MissionClock for the
    /// meta localDate with the *current* workspace timezone (best-effort only).
    /// </summary>
    public const string LegacyMissingWindowPolicy =
        "If StartsAt/EndsAt are missing, evidence uses MissionClock bounds for the mission meta localDate in the current workspace timezone. New missions always persist UTC windows at assignment.";

    public async Task<object> GetTodayAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var tz = await profiles.ResolveTimeZoneAsync(workspaceId, cancellationToken);
        var now = DateTime.UtcNow;
        var clockDate = MissionClock.ToLocalDate(now, tz);
        var localDate = await ResolveStableLocalDateAsync(workspaceId, userId, clockDate, now, cancellationToken);

        await EnsureAssignedAsync(workspaceId, userId, localDate, tz, cancellationToken);
        await ExpireStaleAsync(workspaceId, userId, now, cancellationToken);
        await SyncEvidenceAsync(workspaceId, userId, localDate, cancellationToken);

        var missions = await LoadDayMissionsAsync(workspaceId, userId, localDate, cancellationToken);
        var assignedXp = missions.Sum(m => m.Quest.RewardXp);
        return new
        {
            localDate = localDate.ToString("yyyy-MM-dd"),
            clockLocalDate = clockDate.ToString("yyyy-MM-dd"),
            timeZone = tz,
            maxMissions = DailyMissionCatalog.MaxMissionsPerDay,
            rewardCaps = new { xp = DailyMissionCatalog.MaxDailyMissionXp, coins = DailyMissionCatalog.MaxDailyMissionCoins },
            claimGraceHours = QuestEngine.ClaimGraceAfterEndsAt.TotalHours,
            timezonePolicy =
                "Daily identity freezes to the localDate of any still-valid issued dm window. Changing timezone does not create a second overlapping active set. Evidence for issued missions uses persisted StartsAt/EndsAt. " + LegacyMissingWindowPolicy,
            items = missions.Select(MapMission).ToList(),
            summary = new
            {
                available = missions.Count(m => m.Status == QuestStatus.Active),
                completed = missions.Count(m => m.Status == QuestStatus.Completed),
                claimed = missions.Count(m => m.Status == QuestStatus.Claimed),
                expired = missions.Count(m => m.Status == QuestStatus.Expired),
                assignedRewardXp = assignedXp,
            },
        };
    }

    public async Task<object> SyncAsync(Guid workspaceId, int userId, CancellationToken cancellationToken) =>
        await GetTodayAsync(workspaceId, userId, cancellationToken);

    /// <summary>
    /// If an issued dm window still covers now, freeze to that meta localDate (F4).
    /// </summary>
    public async Task<DateOnly> ResolveStableLocalDateAsync(
        Guid workspaceId,
        int userId,
        DateOnly clockLocalDate,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var active = await db.Quests.AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId
                && q.AssignedUserId == userId
                && q.Category == DailyMissionCatalog.Category
                && q.StartsAt != null && q.EndsAt != null
                && q.StartsAt <= nowUtc && q.EndsAt >= nowUtc)
            .Select(q => q.Description)
            .ToListAsync(cancellationToken);

        foreach (var description in active)
        {
            if (DailyMissionCatalog.TryParseMeta(description, out _, out var frozen))
            {
                return frozen;
            }
        }

        return clockLocalDate;
    }

    public async Task EnsureAssignedAsync(
        Guid workspaceId,
        int userId,
        DateOnly localDate,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        // Multi-instance safety: deterministic PKs + Serializable txn + transient retry.
        // In-memory process locks are NOT relied on across app instances.
        await SqlConcurrency.RunWithClearAsync(db, async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                await EnsureStatsRowAsync(workspaceId, userId, timeZoneId, cancellationToken);
                var existing = await LoadDayMissionsAsync(workspaceId, userId, localDate, cancellationToken);
                if (existing.Count >= DailyMissionCatalog.MaxMissionsPerDay)
                {
                    await tx.CommitAsync(cancellationToken);
                    return;
                }

                var selected = await SelectTemplatesAsync(workspaceId, userId, localDate, existing, cancellationToken);
                var (startUtc, endUtc) = MissionClock.LocalDayUtcBounds(localDate, timeZoneId);
                var now = DateTime.UtcNow;
                var remainingSlots = DailyMissionCatalog.MaxMissionsPerDay - existing.Count;
                var xpBudget = DailyMissionCatalog.MaxDailyMissionXp - existing.Sum(m => m.Quest.RewardXp);
                var coinBudget = DailyMissionCatalog.MaxDailyMissionCoins - existing.Sum(m => m.Quest.RewardCoins);
                var hasTimeBased = existing.Any(m => TimeBasedConditions.Contains(m.Quest.CompletionCondition));

                foreach (var pick in selected)
                {
                    if (remainingSlots <= 0) break;
                    if (TimeBasedConditions.Contains(pick.CompletionCondition))
                    {
                        if (hasTimeBased) continue;
                        hasTimeBased = true;
                    }

                    var questId = DailyMissionIds.QuestId(workspaceId, userId, localDate, pick.TemplateId);
                    if (await db.Quests.AnyAsync(q => q.Id == questId, cancellationToken))
                    {
                        continue;
                    }

                    var target = pick.TargetValue;
                    if (pick.TemplateId == "review_due")
                    {
                        var due = await CountReviewsDueAsync(workspaceId, userId, timeZoneId, cancellationToken);
                        target = Math.Clamp(due <= 0 ? 1 : Math.Min(3, due), 1, 5);
                    }

                    var xp = Math.Min(pick.RewardXp, Math.Max(0, xpBudget));
                    var coins = Math.Min(pick.RewardCoins, Math.Max(0, coinBudget));
                    if (xp <= 0 && coins <= 0 && pick.RewardXp > 0)
                    {
                        continue;
                    }

                    var quest = new QuestDefinition
                    {
                        Id = questId,
                        WorkspaceId = workspaceId,
                        Title = pick.Title,
                        Description = $"{DailyMissionCatalog.MetaPrefix(pick.TemplateId, localDate)} {pick.Description.Replace("{target}", target.ToString(), StringComparison.Ordinal)}",
                        Category = DailyMissionCatalog.Category,
                        Kind = QuestKind.Personalized,
                        StartsAt = startUtc,
                        EndsAt = endUtc,
                        TargetValue = target,
                        RewardXp = xp,
                        RewardCoins = coins,
                        CompletionCondition = pick.CompletionCondition,
                        Visibility = QuestVisibility.Hidden,
                        Difficulty = pick.Difficulty,
                        Status = QuestStatus.Active,
                        AssignedUserId = userId,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.Quests.Add(quest);
                    db.UserQuestProgress.Add(new UserQuestProgress
                    {
                        Id = DailyMissionIds.ProgressId(workspaceId, userId, localDate, pick.TemplateId),
                        WorkspaceId = workspaceId,
                        UserId = userId,
                        QuestId = questId,
                        Progress = 0,
                        Status = QuestStatus.Active,
                        UpdatedAt = now,
                    });
                    xpBudget -= xp;
                    coinBudget -= coins;
                    remainingSlots--;
                }

                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Concurrent insert of same deterministic ids — treat as success
                    db.ChangeTracker.Clear();
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private async Task EnsureStatsRowAsync(Guid workspaceId, int userId, string tz, CancellationToken cancellationToken)
    {
        var stats = await db.UserGameStats.FirstOrDefaultAsync(
            s => s.WorkspaceId == workspaceId && s.UserId == userId, cancellationToken);
        if (stats is not null)
        {
            stats.UpdatedAt = DateTime.UtcNow;
            return;
        }

        db.UserGameStats.Add(new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Level = 1,
            TimeZone = tz,
            UpdatedAt = DateTime.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<List<MissionTemplate>> SelectTemplatesAsync(
        Guid workspaceId,
        int userId,
        DateOnly localDate,
        List<UserQuestProgress> existing,
        CancellationToken cancellationToken)
    {
        var usedToday = existing
            .Select(m => DailyMissionCatalog.TryParseMeta(m.Quest.Description, out var tid, out _) ? tid : "")
            .Where(id => id.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var yesterday = localDate.AddDays(-1);
        var yesterdayTemplates = await db.Quests.AsNoTracking()
            .Where(q => q.WorkspaceId == workspaceId
                && q.AssignedUserId == userId
                && q.Category == DailyMissionCatalog.Category)
            .Select(q => q.Description)
            .ToListAsync(cancellationToken);
        var usedYesterday = yesterdayTemplates
            .Select(d => DailyMissionCatalog.TryParseMeta(d, out var tid, out var d0) && d0 == yesterday ? tid : null)
            .Where(id => id is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

        var tz = await profiles.ResolveTimeZoneAsync(workspaceId, cancellationToken);
        var due = await CountReviewsDueAsync(workspaceId, userId, tz, cancellationToken);
        var weak = await CountWeakMasteryAsync(workspaceId, userId, cancellationToken);

        var ranked = new List<(MissionTemplate T, int Score)>();
        foreach (var t in DailyMissionCatalog.Templates)
        {
            if (usedToday.Contains(t.TemplateId)) continue;
            var score = (int)t.Difficulty;
            if (t.TemplateId == "review_due") score += due > 0 ? 40 : -20;
            if (t.TemplateId is "assessment_try" or "assessment_pass" or "mastery_push")
                score += weak > 0 ? 25 : 5;
            if (usedYesterday.Contains(t.TemplateId)) score -= 15;
            if (t.TemplateId == "study_block") score += 8;
            if (t.TemplateId == "session_focus") score += 10;
            ranked.Add((t, score));
        }

        // Prefer evidence-based over time-based when sorting ties break naturally by score
        return ranked
            .OrderByDescending(x => x.Score)
            .ThenBy(x => TimeBasedConditions.Contains(x.T.CompletionCondition) ? 1 : 0)
            .Select(x => x.T)
            .Take(DailyMissionCatalog.MaxMissionsPerDay * 2) // extra candidates; EnsureAssigned filters time-based
            .ToList();
    }

    private async Task SyncEvidenceAsync(Guid workspaceId, int userId, DateOnly localDate, CancellationToken cancellationToken)
    {
        // Sync every non-terminal daily mission for the learner (not only the frozen localDate set)
        // so persisted StartsAt/EndsAt remain authoritative after timezone changes (F3).
        var missions = await db.UserQuestProgress
            .Include(p => p.Quest)
            .Where(p => p.WorkspaceId == workspaceId
                && p.UserId == userId
                && p.Quest.Category == DailyMissionCatalog.Category
                && p.Quest.AssignedUserId == userId
                && (p.Status == QuestStatus.Active || p.Status == QuestStatus.Completed))
            .ToListAsync(cancellationToken);
        var tz = await profiles.ResolveTimeZoneAsync(workspaceId, cancellationToken);
        var legacyBounds = MissionClock.LocalDayUtcBounds(localDate, tz);

        foreach (var progress in missions)
        {
            if (progress.Status is QuestStatus.Claimed or QuestStatus.Expired or QuestStatus.Cancelled) continue;
            var quest = progress.Quest;

            DateTime startUtc;
            DateTime endUtc;
            if (quest.StartsAt is DateTime s && quest.EndsAt is DateTime e)
            {
                startUtc = s;
                endUtc = e;
            }
            else
            {
                // Legacy fallback: meta localDate in *current* workspace TZ (see LegacyMissingWindowPolicy).
                if (DailyMissionCatalog.TryParseMeta(quest.Description, out _, out var metaDay))
                {
                    (startUtc, endUtc) = MissionClock.LocalDayUtcBounds(metaDay, tz);
                }
                else
                {
                    (startUtc, endUtc) = legacyBounds;
                }
            }

            var value = await MeasureEvidenceAsync(workspaceId, userId, quest.CompletionCondition, startUtc, endUtc, cancellationToken);
            if (value <= progress.Progress) continue;

            progress.Progress = Math.Min(quest.TargetValue, value);
            progress.UpdatedAt = DateTime.UtcNow;
            if (progress.Progress >= quest.TargetValue && progress.Status == QuestStatus.Active)
            {
                progress.Status = QuestStatus.Completed;
                progress.CompletedAt = DateTime.UtcNow;
                db.GameNotifications.Add(new GameNotification
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Kind = GameNotificationKind.QuestCompleted,
                    Title = "ماموریت روزانه تمام شد",
                    Body = quest.Title,
                    PayloadJson = JsonSerializer.Serialize(new { questId = quest.Id, daily = true }, GamificationJson.Options),
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> MeasureEvidenceAsync(
        Guid workspaceId,
        int userId,
        string condition,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        return condition switch
        {
            "review_count" => await db.DomainOutbox.AsNoTracking().CountAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.ReviewCompleted
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken),
            "assessment_count" => await db.DomainOutbox.AsNoTracking().CountAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.AssessmentSubmitted
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken),
            "assessment_passed" => await db.DomainOutbox.AsNoTracking().CountAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.AssessmentPassed
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken),
            "mastery_improved" => await db.DomainOutbox.AsNoTracking().CountAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.MasteryImproved
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken),
            "session_count" => await db.DomainOutbox.AsNoTracking().CountAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.StudySessionCompleted
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken),
            "study_minutes" => await SumStudyMinutesAsync(workspaceId, userId, startUtc, endUtc, cancellationToken),
            "daily_goal" => await db.DomainOutbox.AsNoTracking().AnyAsync(
                e => e.WorkspaceId == workspaceId && e.UserId == userId
                    && e.EventType == DomainEventTypes.StudyGoalCompleted
                    && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc, cancellationToken)
                ? 1 : 0,
            "manual" => 0,
            _ => 0,
        };
    }

    private async Task<int> SumStudyMinutesAsync(
        Guid workspaceId, int userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken)
    {
        var rows = await db.DomainOutbox.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId && e.UserId == userId
                && e.EventType == DomainEventTypes.StudySessionCompleted
                && e.OccurredAt >= startUtc && e.OccurredAt <= endUtc)
            .Select(e => e.PayloadJson)
            .ToListAsync(cancellationToken);
        var sum = 0;
        foreach (var json in rows)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("durationMinutes", out var mins)
                    && mins.TryGetInt32(out var m))
                {
                    sum += Math.Max(0, m);
                }
            }
            catch (JsonException)
            {
            }
        }
        return sum;
    }

    private async Task ExpireStaleAsync(Guid workspaceId, int userId, DateTime now, CancellationToken cancellationToken)
    {
        var staleActive = await db.UserQuestProgress
            .Include(p => p.Quest)
            .Where(p => p.WorkspaceId == workspaceId
                && p.UserId == userId
                && p.Status == QuestStatus.Active
                && p.Quest.Category == DailyMissionCatalog.Category
                && p.Quest.EndsAt != null
                && p.Quest.EndsAt < now)
            .ToListAsync(cancellationToken);
        foreach (var row in staleActive)
        {
            row.Status = QuestStatus.Expired;
            row.UpdatedAt = now;
        }

        var graceCutoff = now - QuestEngine.ClaimGraceAfterEndsAt;
        var staleCompleted = await db.UserQuestProgress
            .Include(p => p.Quest)
            .Where(p => p.WorkspaceId == workspaceId
                && p.UserId == userId
                && p.Status == QuestStatus.Completed
                && p.Quest.Category == DailyMissionCatalog.Category
                && p.Quest.EndsAt != null
                && p.Quest.EndsAt < graceCutoff)
            .ToListAsync(cancellationToken);
        foreach (var row in staleCompleted)
        {
            row.Status = QuestStatus.Expired;
            row.UpdatedAt = now;
        }

        if (staleActive.Count + staleCompleted.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<UserQuestProgress>> LoadDayMissionsAsync(
        Guid workspaceId, int userId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var rows = await db.UserQuestProgress
            .Include(p => p.Quest)
            .Where(p => p.WorkspaceId == workspaceId
                && p.UserId == userId
                && p.Quest.Category == DailyMissionCatalog.Category
                && p.Quest.AssignedUserId == userId)
            .ToListAsync(cancellationToken);

        return rows
            .Where(p => DailyMissionCatalog.TryParseMeta(p.Quest.Description, out _, out var d) && d == localDate)
            .OrderBy(p => p.Quest.CreatedAt)
            .ToList();
    }

    private async Task<int> CountReviewsDueAsync(
        Guid workspaceId, int userId, string timeZoneId, CancellationToken cancellationToken)
    {
        var today = MissionClock.ToLocalDate(DateTime.UtcNow, timeZoneId);
        return await db.SpacedRepetitionCards.AsNoTracking()
            .CountAsync(c => c.WorkspaceId == workspaceId && c.UserId == userId && c.NextReviewDate <= today, cancellationToken);
    }

    private async Task<int> CountWeakMasteryAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        return await db.MasteryNodes.AsNoTracking()
            .CountAsync(n => n.WorkspaceId == workspaceId && n.UserId == userId
                && (n.Level == MasteryLevel.NotStarted || n.Level == MasteryLevel.Learning || n.Level == MasteryLevel.NeedsReview),
                cancellationToken);
    }

    private static object MapMission(UserQuestProgress progress)
    {
        DailyMissionCatalog.TryParseMeta(progress.Quest.Description, out var templateId, out var localDate);
        var template = DailyMissionCatalog.Templates.FirstOrDefault(t => t.TemplateId == templateId);
        var human = progress.Quest.Description;
        var bracket = human.IndexOf(']');
        if (bracket >= 0 && bracket + 1 < human.Length) human = human[(bracket + 1)..].Trim();

        return new
        {
            id = progress.QuestId,
            progressId = progress.Id,
            templateId,
            localDate = localDate == default ? null : localDate.ToString("yyyy-MM-dd"),
            title = progress.Quest.Title,
            description = human,
            why = template?.Why ?? "مأموریت روزانه بر اساس فعالیت یادگیری.",
            pillar = template?.Pillar ?? "ACHIEVE",
            estimatedMinutes = template?.EstimatedMinutes,
            completionCondition = progress.Quest.CompletionCondition,
            targetValue = progress.Quest.TargetValue,
            progress = progress.Progress,
            status = progress.Status.ToString(),
            rewardXp = progress.Quest.RewardXp,
            rewardCoins = progress.Quest.RewardCoins,
            difficulty = progress.Quest.Difficulty,
            startsAt = progress.Quest.StartsAt,
            endsAt = progress.Quest.EndsAt,
            completedAt = progress.CompletedAt,
            claimedAt = progress.ClaimedAt,
            evidenceBased = !string.Equals(progress.Quest.CompletionCondition, "manual", StringComparison.OrdinalIgnoreCase),
            timeBased = TimeBasedConditions.Contains(progress.Quest.CompletionCondition),
        };
    }
}
