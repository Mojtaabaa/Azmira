using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning.Ai;

/// <summary>Builds privacy-minimized learning context for AI prompts. Never includes username/email/password.</summary>
public sealed class AiContextBuilder(AppDbContext db)
{
    public async Task EnsureAuthorizedAsync(Guid workspaceId, int actorUserId, int targetUserId, CancellationToken cancellationToken)
    {
        if (actorUserId == targetUserId)
        {
            var self = await db.WorkspaceMembers.AsNoTracking().AnyAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == actorUserId && item.Status == WorkspaceMemberStatus.Active,
                cancellationToken);
            if (!self) throw new UnauthorizedAccessException("دسترسی به این فضا مجاز نیست.");
            return;
        }

        var actor = await db.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == actorUserId && item.Status == WorkspaceMemberStatus.Active,
            cancellationToken) ?? throw new UnauthorizedAccessException("دسترسی به این فضا مجاز نیست.");

        var elevated = actor.Role is WorkspaceMemberRole.Teacher
            or WorkspaceMemberRole.AcademicManager
            or WorkspaceMemberRole.SchoolAdministrator
            or WorkspaceMemberRole.SchoolOwner;
        if (!elevated) throw new UnauthorizedAccessException("اجازه مشاهده داده یادگیری دیگران را نداری.");

        var targetOk = await db.WorkspaceMembers.AsNoTracking().AnyAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == targetUserId && item.Status == WorkspaceMemberStatus.Active,
            cancellationToken);
        if (!targetOk) throw new UnauthorizedAccessException("کاربر هدف در این فضا نیست.");
    }

    public async Task<AiWorkspaceSettings> GetSettingsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var settings = await db.AiWorkspaceSettings.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        if (settings is not null) return settings;

        var wsFlags = await db.WorkspaceSettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .Select(item => item.FeatureFlags)
            .FirstOrDefaultAsync(cancellationToken) ?? "{}";
        var enabled = false;
        try
        {
            using var doc = JsonDocument.Parse(wsFlags);
            if (doc.RootElement.TryGetProperty("ai", out var ai) && ai.ValueKind == JsonValueKind.True) enabled = true;
            if (doc.RootElement.TryGetProperty("aiCoach", out var coach) && coach.ValueKind == JsonValueKind.True) enabled = true;
        }
        catch
        {
            // ignore
        }

        settings = new AiWorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Enabled = enabled,
            ProviderCode = "heuristic",
            Model = "",
            AllowUnderAge = false,
            MinAgeYears = 13,
            SystemPromptExtra = "",
            UpdatedAt = DateTime.UtcNow,
        };
        db.AiWorkspaceSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<object> BuildFactsAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var weekStart = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.DayOfWeek + 6) % 7);
        var prevStart = weekStart.AddDays(-7);

        var sessions = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Collection == "sessions")
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);

        var studyMinutes = SumMinutes(sessions, weekStart, DateTime.UtcNow.AddDays(1));
        var prevMinutes = SumMinutes(sessions, prevStart, weekStart);

        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Topic != null)
            .OrderBy(item => item.ScoreEma)
            .ToListAsync(cancellationToken);

        var weakest = mastery.FirstOrDefault()?.Subject ?? "ریاضی";
        var strongest = mastery.OrderByDescending(item => item.ScoreEma).FirstOrDefault()?.Subject ?? "علوم";

        var attempts = await db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.SubmittedAt >= weekStart)
            .Select(item => new { item.Percent, item.Assessment.Subject })
            .ToListAsync(cancellationToken);

        var overdue = await db.SpacedRepetitionCards.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.NextReviewDate <= DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);

        var questsDue = await db.UserQuestProgress.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Status == QuestStatus.Active, cancellationToken);

        var pendingTasks = await db.StudyRecords.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Collection == "tasks", cancellationToken);

        return new
        {
            studyMinutes,
            prevStudyMinutes = prevMinutes,
            weakestSubject = weakest,
            strongestSubject = strongest,
            weeklyAttemptCount = attempts.Count,
            weeklyAveragePercent = attempts.Count == 0 ? 0 : Math.Round(attempts.Average(item => item.Percent), 1),
            overdueReviews = overdue,
            activeQuests = questsDue,
            pendingTasks,
            lowMasteryTopics = mastery.Take(5).Select(item => new
            {
                item.Subject,
                item.Chapter,
                item.Topic,
                level = item.Level.ToString(),
                item.ScoreEma,
            }),
        };
    }

    public static bool PassesAgeGate(AiWorkspaceSettings settings, JsonElement? userSettingsData)
    {
        if (settings.AllowUnderAge) return true;
        if (userSettingsData is null) return true; // unknown age: allow heuristic but providers should still be cautious
        if (userSettingsData.Value.TryGetProperty("birthYear", out var year) && year.TryGetInt32(out var y))
        {
            var age = DateTime.UtcNow.Year - y;
            return age >= settings.MinAgeYears;
        }

        if (userSettingsData.Value.TryGetProperty("ageYears", out var ageEl) && ageEl.TryGetInt32(out var ageYears))
        {
            return ageYears >= settings.MinAgeYears;
        }

        return true;
    }

    private static int SumMinutes(List<string> payloads, DateTime from, DateTime to)
    {
        var total = 0;
        foreach (var raw in payloads)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var minutes = 0;
                if (doc.RootElement.TryGetProperty("durationMinutes", out var m) && m.TryGetInt32(out var mv)) minutes = mv;
                else if (doc.RootElement.TryGetProperty("minutes", out var m2) && m2.TryGetInt32(out var mv2)) minutes = mv2;

                DateTime? at = null;
                if (doc.RootElement.TryGetProperty("endedAt", out var e) && e.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(e.GetString(), out var ended)) at = ended.ToUniversalTime();
                else if (doc.RootElement.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String
                    && DateTime.TryParse(d.GetString(), out var date)) at = date.ToUniversalTime();

                if (at is DateTime when && when >= from && when < to) total += Math.Max(0, minutes);
                else if (at is null) total += Math.Max(0, minutes); // legacy rows without date: count cautiously once
            }
            catch
            {
                // skip bad payloads
            }
        }

        return total;
    }
}
