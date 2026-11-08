using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class LeaderboardService(AppDbContext db)
{
    private const int MaxPageSize = 50;

    public async Task<object> GetBoardAsync(
        Guid workspaceId,
        LeaderboardMetric metric,
        string period,
        string? subject,
        Guid? classId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize);

        var rows = await BuildRowsAsync(workspaceId, metric, period, subject, classId, cancellationToken);
        var total = rows.Count;
        var pageRows = rows.Skip((page - 1) * pageSize).Take(pageSize)
            .Select((row, index) => new
            {
                rank = (page - 1) * pageSize + index + 1,
                userId = row.UserId,
                teamId = row.TeamId,
                displayName = row.DisplayName,
                score = row.Score,
                firstAchievedAt = row.FirstAchievedAt,
            })
            .ToList();

        var meta = DescribeMetric(metric);
        return new
        {
            metric = metric.ToString(),
            period,
            subject,
            classId,
            page,
            pageSize,
            total,
            entries = pageRows,
            viewKind = meta.ViewKind,
            labelFa = meta.LabelFa,
            labelEn = meta.LabelEn,
            privacyDefault = "private",
            peerComparison = meta.PeerComparison,
            note = meta.Note,
        };
    }

    private static (string ViewKind, string LabelFa, string LabelEn, bool PeerComparison, string Note) DescribeMetric(LeaderboardMetric metric) =>
        metric switch
        {
            LeaderboardMetric.LearningImprovement => (
                "peerComparison",
                "بهبود یادگیری (مقایسه هم‌کلاسی — اختیاری)",
                "Learning improvement (peer)",
                true,
                "این نمای گروهی است؛ نمای پیش‌فرض محصول، بهبود شخصی نسبت به معیار خودت است (/improvement/me)."),
            LeaderboardMetric.WeeklyXp or LeaderboardMetric.MonthlyXp or LeaderboardMetric.School
                or LeaderboardMetric.Class or LeaderboardMetric.Team or LeaderboardMetric.QuestCompletion => (
                "engagement",
                "فعالیت و دستاورد",
                "Engagement / achievement activity",
                true,
                "امتیاز فعالیت است و با بهبود شخصی (تسلط/ارزیابی) یکی نیست."),
            LeaderboardMetric.Consistency or LeaderboardMetric.GoalCompletion or LeaderboardMetric.SubjectPerformance => (
                "engagement",
                metric.ToString(),
                metric.ToString(),
                true,
                "جدول رتبه‌بندی اختیاری است؛ نمایش پیش‌فرض خصوصی است."),
            _ => ("engagement", metric.ToString(), metric.ToString(), true, "جدول رتبه‌بندی اختیاری است."),
        };

    public async Task<object> SnapshotAsync(
        Guid workspaceId,
        LeaderboardMetric metric,
        string period,
        string? subject,
        Guid? classId,
        CancellationToken cancellationToken)
    {
        var rows = await BuildRowsAsync(workspaceId, metric, period, subject, classId, cancellationToken);
        var periodKey = PeriodKey(period, DateTime.UtcNow);
        var payload = JsonSerializer.Serialize(rows.Select((row, i) => new
        {
            rank = i + 1,
            row.UserId,
            row.TeamId,
            row.DisplayName,
            row.Score,
            row.FirstAchievedAt,
        }), GamificationJson.Options);

        var snap = new LeaderboardSnapshot
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Metric = metric,
            PeriodKey = periodKey,
            Subject = subject,
            ClassId = classId,
            PayloadJson = payload,
            GeneratedAt = DateTime.UtcNow,
        };
        db.LeaderboardSnapshots.Add(snap);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = snap.Id, periodKey, generatedAt = snap.GeneratedAt, entries = rows.Count };
    }

    public async Task<object> ListSnapshotsAsync(
        Guid workspaceId,
        LeaderboardMetric? metric,
        string? period,
        CancellationToken cancellationToken)
    {
        var query = db.LeaderboardSnapshots.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
        if (metric is LeaderboardMetric m) query = query.Where(item => item.Metric == m);
        if (!string.IsNullOrWhiteSpace(period))
        {
            var prefix = period.Trim().ToLowerInvariant();
            query = query.Where(item => item.PeriodKey.StartsWith(prefix) || item.PeriodKey.Contains(prefix));
        }

        var items = await query.OrderByDescending(item => item.GeneratedAt).Take(50)
            .Select(item => new
            {
                id = item.Id,
                metric = item.Metric.ToString(),
                periodKey = item.PeriodKey,
                subject = item.Subject,
                classId = item.ClassId,
                generatedAt = item.GeneratedAt,
                payload = item.PayloadJson,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> GetPrivacyAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var row = await EnsurePrivacyAsync(workspaceId, userId, cancellationToken);
        return new
        {
            leaderboardVisible = row.LeaderboardVisible,
            showRealName = row.ShowRealName,
            privacyDefault = "private",
            note = "نمایش در جدول‌های گروهی به‌صورت پیش‌فرض خاموش است؛ بهبود شخصی از /improvement/me است.",
        };
    }

    public async Task<object> UpdatePrivacyAsync(
        Guid workspaceId,
        int userId,
        bool? leaderboardVisible,
        bool? showRealName,
        CancellationToken cancellationToken)
    {
        var row = await EnsurePrivacyAsync(workspaceId, userId, cancellationToken);
        if (leaderboardVisible is bool visible) row.LeaderboardVisible = visible;
        if (showRealName is bool show) row.ShowRealName = show;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new { leaderboardVisible = row.LeaderboardVisible, showRealName = row.ShowRealName };
    }

    private async Task<UserPrivacySettings> EnsurePrivacyAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var row = await db.UserPrivacySettings.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (row is not null) return row;

        var workspace = await db.Workspaces.AsNoTracking().FirstAsync(item => item.Id == workspaceId, cancellationToken);
        row = new UserPrivacySettings
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            LeaderboardVisible = false,
            ShowRealName = workspace.Type == WorkspaceType.Personal,
            UpdatedAt = DateTime.UtcNow,
        };
        db.UserPrivacySettings.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private async Task<List<RankRow>> BuildRowsAsync(
        Guid workspaceId,
        LeaderboardMetric metric,
        string period,
        string? subject,
        Guid? classId,
        CancellationToken cancellationToken)
    {
        var memberIds = await VisibleMemberIdsAsync(workspaceId, classId, metric, cancellationToken);
        if (memberIds.Count == 0) return [];

        var (from, to) = PeriodRange(period);
        List<RankRow> raw = metric switch
        {
            LeaderboardMetric.WeeklyXp or LeaderboardMetric.MonthlyXp or LeaderboardMetric.School
                => await XpRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
            LeaderboardMetric.Consistency => await ConsistencyRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
            LeaderboardMetric.GoalCompletion => await GoalRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
            LeaderboardMetric.LearningImprovement => await ImprovementRowsAsync(workspaceId, memberIds, subject, cancellationToken),
            LeaderboardMetric.SubjectPerformance => await SubjectPerfRowsAsync(workspaceId, memberIds, subject, from, to, cancellationToken),
            LeaderboardMetric.QuestCompletion => await QuestRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
            LeaderboardMetric.Team => await TeamRowsAsync(workspaceId, classId, from, to, cancellationToken),
            LeaderboardMetric.Class => await XpRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
            _ => await XpRowsAsync(workspaceId, memberIds, from, to, cancellationToken),
        };

        var privacy = await db.UserPrivacySettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && memberIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var users = await db.Users.AsNoTracking()
            .Where(user => memberIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        foreach (var row in raw.Where(item => item.UserId > 0))
        {
            privacy.TryGetValue(row.UserId, out var settings);
            var showName = settings?.ShowRealName ?? false;
            if (showName && users.TryGetValue(row.UserId, out var user) && !string.IsNullOrWhiteSpace(user.DisplayName))
            {
                row.DisplayName = user.DisplayName!;
            }
            else
            {
                row.DisplayName = $"بازیکن #{row.UserId}";
            }
        }

        return raw
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.FirstAchievedAt)
            .ThenBy(item => item.UserId)
            .ThenBy(item => item.TeamId ?? Guid.Empty)
            .ToList();
    }

    private async Task<HashSet<int>> VisibleMemberIdsAsync(
        Guid workspaceId,
        Guid? classId,
        LeaderboardMetric metric,
        CancellationToken cancellationToken)
    {
        IQueryable<int> baseQuery;
        if (metric == LeaderboardMetric.Class || classId is Guid cid)
        {
            if (classId is not Guid classGuid) return [];
            var belongs = await db.Classes.AsNoTracking()
                .AnyAsync(item => item.Id == classGuid && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken);
            if (!belongs) return [];
            baseQuery = db.ClassMembers.AsNoTracking()
                .Where(item => item.ClassId == classGuid)
                .Select(item => item.UserId);
        }
        else
        {
            baseQuery = db.WorkspaceMembers.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.Status == WorkspaceMemberStatus.Active)
                .Select(item => item.UserId);
        }

        var ids = await baseQuery.Distinct().ToListAsync(cancellationToken);
        // Opt-in: missing privacy row = private (Phase 12 default). Only LeaderboardVisible=true appear.
        var visible = await db.UserPrivacySettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.LeaderboardVisible)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        var visibleSet = visible.ToHashSet();
        return ids.Where(id => visibleSet.Contains(id)).ToHashSet();
    }

    private async Task<List<RankRow>> XpRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var grouped = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && memberIds.Contains(item.UserId)
                && item.Kind == LedgerKind.Earn
                && item.CreatedAt >= from
                && item.CreatedAt < to)
            .GroupBy(item => item.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Score = group.Sum(item => item.Delta),
                FirstAchievedAt = group.Min(item => item.CreatedAt),
            })
            .ToListAsync(cancellationToken);

        return grouped.Select(item => new RankRow
        {
            UserId = item.UserId,
            Score = item.Score,
            FirstAchievedAt = item.FirstAchievedAt,
        }).ToList();
    }

    private async Task<List<RankRow>> ConsistencyRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);
        var days = await db.StreakDays.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && memberIds.Contains(item.UserId)
                && item.LocalDate >= fromDate
                && item.LocalDate < toDate
                && item.Kind == StreakDayKind.Study)
            .GroupBy(item => item.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Score = group.Select(item => item.LocalDate).Distinct().Count(),
                FirstAchievedAt = group.Min(item => item.LocalDate),
            })
            .ToListAsync(cancellationToken);

        return days.Select(item => new RankRow
        {
            UserId = item.UserId,
            Score = item.Score,
            FirstAchievedAt = item.FirstAchievedAt.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        }).ToList();
    }

    private async Task<List<RankRow>> GoalRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var rows = await db.DomainOutbox.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && item.UserId != null
                && memberIds.Contains(item.UserId.Value)
                && item.EventType == DomainEventTypes.StudyGoalCompleted
                && item.OccurredAt >= from
                && item.OccurredAt < to)
            .GroupBy(item => item.UserId!.Value)
            .Select(group => new
            {
                UserId = group.Key,
                Score = group.Count(),
                FirstAchievedAt = group.Min(item => item.OccurredAt),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new RankRow
        {
            UserId = item.UserId,
            Score = item.Score,
            FirstAchievedAt = item.FirstAchievedAt,
        }).ToList();
    }

    private async Task<List<RankRow>> QuestRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var rows = await db.UserQuestProgress.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && memberIds.Contains(item.UserId)
                && item.CompletedAt != null
                && item.CompletedAt >= from
                && item.CompletedAt < to)
            .GroupBy(item => item.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                Score = group.Count(),
                FirstAchievedAt = group.Min(item => item.CompletedAt!.Value),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => new RankRow
        {
            UserId = item.UserId,
            Score = item.Score,
            FirstAchievedAt = item.FirstAchievedAt,
        }).ToList();
    }

    private async Task<List<RankRow>> SubjectPerfRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        string? subject,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject)) return [];
        var tests = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && memberIds.Contains(item.UserId)
                && item.Collection == "tests")
            .Select(item => new { item.UserId, item.Data, item.Seq })
            .ToListAsync(cancellationToken);

        var best = new Dictionary<int, RankRow>();
        foreach (var test in tests)
        {
            if (!TryParseTest(test.Data, out var sub, out var score, out _)) continue;
            if (!string.Equals(sub, subject, StringComparison.OrdinalIgnoreCase)) continue;
            if (score < 0 || score > 100) continue;
            if (!best.TryGetValue(test.UserId, out var existing) || score > existing.Score)
            {
                best[test.UserId] = new RankRow
                {
                    UserId = test.UserId,
                    Score = (int)Math.Round(score),
                    FirstAchievedAt = DateTime.UnixEpoch.AddSeconds(test.Seq),
                };
            }
        }

        return best.Values.ToList();
    }

    private async Task<List<RankRow>> ImprovementRowsAsync(
        Guid workspaceId,
        HashSet<int> memberIds,
        string? subject,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject)) return [];
        var tests = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && memberIds.Contains(item.UserId)
                && item.Collection == "tests")
            .OrderByDescending(item => item.Seq)
            .Select(item => new { item.UserId, item.Data, item.Seq })
            .ToListAsync(cancellationToken);

        var byUser = tests.GroupBy(item => item.UserId);
        var rows = new List<RankRow>();
        foreach (var group in byUser)
        {
            var sameSubject = new List<(double Score, int Seq)>();
            foreach (var test in group)
            {
                if (!TryParseTest(test.Data, out var sub, out var score, out _)) continue;
                if (!string.Equals(sub, subject, StringComparison.OrdinalIgnoreCase)) continue;
                if (score < 0 || score > 100) continue;
                sameSubject.Add((score, test.Seq));
            }

            if (sameSubject.Count < 2) continue;
            var recent = sameSubject[0];
            var baselineScores = sameSubject.Skip(1).Take(5).Select(item => item.Score).ToList();
            if (baselineScores.Count == 0) continue;
            var baseline = baselineScores.Average();
            var improvement = recent.Score - baseline;
            rows.Add(new RankRow
            {
                UserId = group.Key,
                Score = (int)Math.Round(improvement * 100),
                FirstAchievedAt = DateTime.UnixEpoch.AddSeconds(recent.Seq),
            });
        }

        return rows;
    }

    private async Task<List<RankRow>> TeamRowsAsync(
        Guid workspaceId,
        Guid? classId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var teamsQuery = db.Teams.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null);
        if (classId is Guid cid) teamsQuery = teamsQuery.Where(item => item.ClassId == cid);
        var teams = await teamsQuery.Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);
        if (teams.Count == 0) return [];

        var teamIds = teams.Select(item => item.Id).ToList();
        var members = await db.TeamMembers.AsNoTracking()
            .Where(item => teamIds.Contains(item.TeamId) && item.LeftAt == null)
            .ToListAsync(cancellationToken);

        var rows = new List<RankRow>();
        foreach (var team in teams)
        {
            var memberships = members.Where(item => item.TeamId == team.Id).ToList();
            var score = 0;
            var first = DateTime.MaxValue;
            foreach (var member in memberships)
            {
                var xpQuery = db.XpLedger.AsNoTracking()
                    .Where(item => item.WorkspaceId == workspaceId
                        && item.UserId == member.UserId
                        && item.Kind == LedgerKind.Earn
                        && item.CreatedAt >= from
                        && item.CreatedAt < to
                        && item.CreatedAt >= member.JoinedAt);
                var sum = await xpQuery.SumAsync(item => (int?)item.Delta, cancellationToken) ?? 0;
                var minAt = await xpQuery.Select(item => (DateTime?)item.CreatedAt).MinAsync(cancellationToken);
                score += sum;
                if (minAt is DateTime at && at < first) first = at;
            }

            rows.Add(new RankRow
            {
                TeamId = team.Id,
                DisplayName = team.Name,
                Score = score,
                FirstAchievedAt = first == DateTime.MaxValue ? DateTime.UtcNow : first,
            });
        }

        return rows;
    }

    private static bool TryParseTest(string raw, out string subject, out double score, out DateTime? at)
    {
        subject = "";
        score = 0;
        at = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            subject = doc.RootElement.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? ""
                : "";
            if (!doc.RootElement.TryGetProperty("score", out var sc) || !sc.TryGetDouble(out score)) return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static (DateTime From, DateTime To) PeriodRange(string period)
    {
        var now = DateTime.UtcNow;
        var code = (period ?? "week").Trim().ToLowerInvariant();
        return code switch
        {
            "day" or "daily" => (now.Date, now.Date.AddDays(1)),
            "month" or "monthly" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1)),
            "all" or "all-time" or "alltime" => (DateTime.UnixEpoch, now.AddDays(1)),
            _ => (StartOfWeek(now), StartOfWeek(now).AddDays(7)),
        };
    }

    private static string PeriodKey(string period, DateTime now)
    {
        var code = (period ?? "week").Trim().ToLowerInvariant();
        return code switch
        {
            "day" or "daily" => $"day:{now:yyyy-MM-dd}",
            "month" or "monthly" => $"month:{now:yyyy-MM}",
            "all" or "all-time" or "alltime" => "all",
            _ => $"week:{now:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(now):00}",
        };
    }

    private static DateTime StartOfWeek(DateTime now)
    {
        var date = now.Date;
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday start
        return DateTime.SpecifyKind(date.AddDays(-diff), DateTimeKind.Utc);
    }

    private sealed class RankRow
    {
        public int UserId { get; set; }
        public Guid? TeamId { get; set; }
        public string DisplayName { get; set; } = "";
        public int Score { get; set; }
        public DateTime FirstAchievedAt { get; set; }
    }
}
