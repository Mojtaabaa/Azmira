using System.Globalization;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

/// <summary>
/// Self-first personal improvement vs own baseline.
/// Signals: mastery EMA, assessment %, successful SRS — never XP/time/login streaks alone.
/// </summary>
public sealed class PersonalImprovementService(AppDbContext db)
{
    public const int MinEvidenceEvents = 5;
    public const int BaselineWindowDays = 14;
    public const int InactivityRebaselineDays = 30;

    public async Task<object> GetMeAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var events = await CollectEvidenceAsync(workspaceId, userId, cancellationToken);
        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .ToListAsync(cancellationToken);

        var subjects = mastery
            .Select(item => item.Subject)
            .Concat(events.Select(item => item.Subject))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item)
            .ToList();

        var subjectRows = new List<object>();
        foreach (var subject in subjects)
        {
            subjectRows.Add(await BuildSubjectAsync(workspaceId, userId, subject, events, mastery, persist: true, cancellationToken));
        }

        var composite = await BuildSubjectAsync(workspaceId, userId, "", events, mastery, persist: true, cancellationToken);
        await MaterializeCurrentWeekAsync(workspaceId, userId, "", composite, cancellationToken);

        var snapshots = await db.ImprovementSnapshots.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.SubjectKey == "")
            .OrderBy(item => item.PeriodKey)
            .Take(16)
            .Select(item => new
            {
                periodKey = item.PeriodKey,
                absoluteScore = item.AbsoluteScore,
                improvementPercent = item.ImprovementPercent,
                evidenceEventCount = item.EvidenceEventCount,
                capturedAt = item.CapturedAt,
            })
            .ToListAsync(cancellationToken);

        return new
        {
            viewKind = "personalImprovement",
            scoringPolicy = "Self-first vs personal baseline; mastery/assessment/SRS only — never XP/time/login streaks.",
            privacyDefault = "private",
            coldStartDays = BaselineWindowDays,
            minEvidenceEvents = MinEvidenceEvents,
            composite,
            subjects = subjectRows,
            sparkline = snapshots,
            sparklineSource = "weeklySnapshotsOnRead",
            sparklineNote = "نقاط اسپارک‌لاین از عکس‌های هفتگی هنگام خواندن API هستند، نه بازسازی تاریخچهٔ تسلط.",
            rejectedSignals = new[] { "xp", "timeOnApp", "loginStreak" },
        };
    }

    public async Task<object> RebaselineAsync(Guid workspaceId, int userId, bool consent, string? subjectKey, CancellationToken cancellationToken)
    {
        if (!consent)
            throw new InvalidOperationException("بازنشانی معیار پایه نیاز به رضایت صریح دارد.");

        var key = subjectKey?.Trim() ?? "";
        var existing = await db.PersonalBaselines
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.SubjectKey == key)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            db.PersonalBaselines.RemoveRange(existing);
            await db.SaveChangesAsync(cancellationToken);
        }

        var events = await CollectEvidenceAsync(workspaceId, userId, cancellationToken);
        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .ToListAsync(cancellationToken);

        var row = await BuildSubjectAsync(workspaceId, userId, key, events, mastery, persist: true, cancellationToken);
        return new { rebaselined = true, subjectKey = key, result = row };
    }

    private async Task<object> BuildSubjectAsync(
        Guid workspaceId,
        int userId,
        string subjectKey,
        List<EvidenceEvent> allEvents,
        List<MasteryNode> mastery,
        bool persist,
        CancellationToken cancellationToken)
    {
        var subjectEvents = string.IsNullOrEmpty(subjectKey)
            ? allEvents
            : allEvents.Where(item => string.Equals(item.Subject, subjectKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var subjectMastery = string.IsNullOrEmpty(subjectKey)
            ? mastery
            : mastery.Where(item => string.Equals(item.Subject, subjectKey, StringComparison.OrdinalIgnoreCase)).ToList();

        var absolute = AbsoluteScore(subjectMastery, subjectEvents);
        if (subjectEvents.Count == 0 && subjectMastery.Count == 0)
        {
            return new
            {
                subjectKey = string.IsNullOrEmpty(subjectKey) ? null : subjectKey,
                status = "NoData",
                message = "هنوز شواهد یادگیری (تسلط / ارزیابی / بازبینی موفق) ثبت نشده است.",
                absoluteScore = 0d,
                baselineScore = (double?)null,
                improvementPercent = (double?)null,
                evidenceEventCount = 0,
                baseline = (object?)null,
            };
        }

        var firstAt = subjectEvents.Count > 0
            ? subjectEvents.Min(item => item.At)
            : subjectMastery.Min(item => item.LastEvidenceAt ?? item.UpdatedAt);
        var windowEnd = firstAt.AddDays(BaselineWindowDays);
        var windowEvents = subjectEvents.Where(item => item.At >= firstAt && item.At <= windowEnd).ToList();

        if (windowEvents.Count < MinEvidenceEvents)
        {
            return new
            {
                subjectKey = string.IsNullOrEmpty(subjectKey) ? null : subjectKey,
                status = "Collecting",
                message = "در حال جمع‌آوری معیار پایه",
                absoluteScore = absolute,
                baselineScore = (double?)null,
                improvementPercent = (double?)null,
                evidenceEventCount = windowEvents.Count,
                evidenceNeeded = MinEvidenceEvents - windowEvents.Count,
                baselineWindow = new { start = firstAt, end = windowEnd },
                baseline = (object?)null,
            };
        }

        var baselineScores = windowEvents.Select(item => item.Score01).ToList();
        var baselineMean = baselineScores.Average();

        PersonalBaseline? stored = null;
        if (persist)
        {
            stored = await db.PersonalBaselines
                .FirstOrDefaultAsync(
                    item => item.WorkspaceId == workspaceId && item.UserId == userId && item.SubjectKey == subjectKey,
                    cancellationToken);

            var lastEvidence = subjectEvents.Max(item => item.At);
            var inactive = (DateTime.UtcNow - lastEvidence).TotalDays >= InactivityRebaselineDays;

            if (stored is null)
            {
                stored = new PersonalBaseline
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    SubjectKey = subjectKey,
                    WindowStart = firstAt,
                    WindowEnd = windowEnd,
                    BaselineScore = baselineMean,
                    EvidenceEventCount = windowEvents.Count,
                    EvidenceSummaryJson = JsonSerializer.Serialize(windowEvents.Take(12).Select(AsRef)),
                    EstablishedAt = DateTime.UtcNow,
                };
                db.PersonalBaselines.Add(stored);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (inactive && stored.RebaselinedAt is null)
            {
                // Soft hint only — rebaseline requires explicit consent via API.
            }
        }

        var baselineValue = stored?.BaselineScore ?? baselineMean;
        var denom = Math.Max(baselineValue, 0.01);
        var improvementPercent = ((absolute - baselineValue) / denom) * 100.0;

        return new
        {
            subjectKey = string.IsNullOrEmpty(subjectKey) ? null : subjectKey,
            status = "Ready",
            message = (string?)null,
            absoluteScore = Math.Round(absolute, 4),
            baselineScore = Math.Round(baselineValue, 4),
            improvementPercent = Math.Round(improvementPercent, 2),
            evidenceEventCount = subjectEvents.Count,
            baseline = stored is null ? null : new
            {
                id = stored.Id,
                windowStart = stored.WindowStart,
                windowEnd = stored.WindowEnd,
                score = stored.BaselineScore,
                evidenceEventCount = stored.EvidenceEventCount,
                establishedAt = stored.EstablishedAt,
                rebaselinedAt = stored.RebaselinedAt,
            },
            evidenceSample = windowEvents.Take(8).Select(AsRef).ToList(),
        };
    }

    private async Task MaterializeCurrentWeekAsync(
        Guid workspaceId,
        int userId,
        string subjectKey,
        object composite,
        CancellationToken cancellationToken)
    {
        var doc = JsonSerializer.SerializeToElement(composite);
        if (!doc.TryGetProperty("status", out var statusEl) || statusEl.GetString() != "Ready") return;
        if (!doc.TryGetProperty("absoluteScore", out var absEl)) return;
        if (!doc.TryGetProperty("improvementPercent", out var impEl)) return;

        var periodKey = WeekKey(DateTime.UtcNow);
        var existing = await db.ImprovementSnapshots
            .FirstOrDefaultAsync(
                item => item.WorkspaceId == workspaceId
                    && item.UserId == userId
                    && item.SubjectKey == subjectKey
                    && item.PeriodKey == periodKey,
                cancellationToken);

        var evidenceCount = doc.TryGetProperty("evidenceEventCount", out var ev) && ev.TryGetInt32(out var n) ? n : 0;
        if (existing is null)
        {
            db.ImprovementSnapshots.Add(new ImprovementSnapshot
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                SubjectKey = subjectKey,
                PeriodKey = periodKey,
                AbsoluteScore = absEl.GetDouble(),
                ImprovementPercent = impEl.GetDouble(),
                EvidenceEventCount = evidenceCount,
                CapturedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.AbsoluteScore = absEl.GetDouble();
            existing.ImprovementPercent = impEl.GetDouble();
            existing.EvidenceEventCount = evidenceCount;
            existing.CapturedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<EvidenceEvent>> CollectEvidenceAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var list = new List<EvidenceEvent>();

        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.EvidenceCount > 0)
            .Select(item => new { item.Id, item.Subject, item.Topic, item.ScoreEma, item.LastEvidenceAt, item.UpdatedAt, item.EvidenceCount })
            .ToListAsync(cancellationToken);
        foreach (var node in mastery)
        {
            var at = node.LastEvidenceAt ?? node.UpdatedAt;
            list.Add(new EvidenceEvent(
                "MasteryNode",
                node.Id.ToString("N"),
                node.Subject,
                Math.Clamp(node.ScoreEma, 0, 1),
                at,
                $"تسلط {node.Subject}/{(node.Topic ?? "—")} EMA={node.ScoreEma:0.00} (n={node.EvidenceCount})"));
        }

        var attempts = await db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && (item.Status == AttemptStatus.Submitted || item.Status == AttemptStatus.Scored)
                && item.SubmittedAt != null)
            .Join(
                db.Assessments.AsNoTracking().Where(a => a.WorkspaceId == workspaceId),
                attempt => attempt.AssessmentId,
                assessment => assessment.Id,
                (attempt, assessment) => new { attempt.Id, assessment.Subject, attempt.Percent, attempt.SubmittedAt })
            .ToListAsync(cancellationToken);
        foreach (var row in attempts)
        {
            list.Add(new EvidenceEvent(
                "AssessmentAttempt",
                row.Id.ToString("N"),
                row.Subject,
                Math.Clamp(row.Percent / 100.0, 0, 1),
                row.SubmittedAt!.Value,
                $"ارزیابی {row.Subject} ‎{row.Percent:0.#}%"));
        }

        var reviews = await db.SpacedRepetitionReviews.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Successful)
            .Join(
                db.SpacedRepetitionCards.AsNoTracking().Where(c => c.WorkspaceId == workspaceId && c.UserId == userId),
                review => review.CardId,
                card => card.Id,
                (review, card) => new { review.Id, card.Subject, card.Topic, review.ReviewedAt })
            .ToListAsync(cancellationToken);
        foreach (var row in reviews)
        {
            list.Add(new EvidenceEvent(
                "SrsReview",
                row.Id.ToString("N"),
                row.Subject,
                0.7,
                row.ReviewedAt,
                $"بازبینی موفق {row.Subject}/{(row.Topic ?? "—")}"));
        }

        return list.OrderBy(item => item.At).ToList();
    }

    private static double AbsoluteScore(List<MasteryNode> mastery, List<EvidenceEvent> events)
    {
        if (mastery.Count > 0)
            return Math.Clamp(mastery.Average(item => item.ScoreEma), 0, 1);

        var scored = events.Where(item => item.Type is "AssessmentAttempt" or "MasteryNode").ToList();
        if (scored.Count > 0) return Math.Clamp(scored.Average(item => item.Score01), 0, 1);
        if (events.Count > 0) return Math.Clamp(events.Average(item => item.Score01), 0, 1);
        return 0;
    }

    private static object AsRef(EvidenceEvent item) => new
    {
        type = item.Type,
        id = item.Id,
        subject = item.Subject,
        score01 = item.Score01,
        at = item.At,
        detail = item.Detail,
    };

    private static string WeekKey(DateTime utc) =>
        $"{utc:yyyy}-W{ISOWeek.GetWeekOfYear(utc):00}";

    private sealed record EvidenceEvent(string Type, string Id, string Subject, double Score01, DateTime At, string Detail);
}
