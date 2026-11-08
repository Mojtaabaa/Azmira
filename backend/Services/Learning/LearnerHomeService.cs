using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

/// <summary>Composed learner home DTO — single round-trip for Home dashboard.</summary>
public sealed class LearnerHomeService(
    AppDbContext db,
    GamificationQueryService gamification,
    DailyMissionService missions,
    JourneyService journeys,
    PersonalImprovementService improvement)
{
    public async Task<object> GetHomeAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var profileTask = Safe(() => gamification.GetProfileAsync(workspaceId, userId, cancellationToken));
        var missionsTask = Safe(() => missions.GetTodayAsync(workspaceId, userId, cancellationToken));
        var journeyTask = Safe(() => journeys.GetMeAsync(workspaceId, userId, cancellationToken));
        var improvementTask = Safe(() => improvement.GetMeAsync(workspaceId, userId, cancellationToken));

        await Task.WhenAll(profileTask, missionsTask, journeyTask, improvementTask);

        var profile = await profileTask;
        var daily = await missionsTask;
        var journey = await journeyTask;
        var improve = await improvementTask;

        var reviewsDue = await db.SpacedRepetitionCards.AsNoTracking()
            .CountAsync(
                c => c.WorkspaceId == workspaceId
                    && c.UserId == userId
                    && c.NextReviewDate <= DateOnly.FromDateTime(DateTime.UtcNow),
                cancellationToken);

        var weakMastery = await db.MasteryNodes.AsNoTracking()
            .CountAsync(
                n => n.WorkspaceId == workspaceId
                    && n.UserId == userId
                    && n.Level <= MasteryLevel.NeedsReview,
                cancellationToken);

        return new
        {
            composedAt = DateTime.UtcNow,
            streakSource = "server",
            streakPolicy = "Home uses server streak only; local session-derived streak is deprecated on Home.",
            privacy = new
            {
                leaderboardDefault = "private",
                improvementView = "self-first",
            },
            profile,
            dailyMissions = daily,
            journeySummary = journey is null ? null : SummarizeJourney(journey),
            improvementPulse = improve is null ? null : SummarizeImprovement(improve),
            reviewsDue,
            weakMasteryCount = weakMastery,
        };
    }

    private static object? SummarizeJourney(object journey)
    {
        try
        {
            var el = System.Text.Json.JsonSerializer.SerializeToElement(journey);
            return new
            {
                journeyId = el.TryGetProperty("journeyId", out var id) ? id.GetGuid() : (Guid?)null,
                title = el.TryGetProperty("title", out var t) ? t.GetString() : null,
                isBootstrap = el.TryGetProperty("isBootstrap", out var b) && b.ValueKind == System.Text.Json.JsonValueKind.True,
                summary = el.TryGetProperty("summary", out var s) ? System.Text.Json.JsonSerializer.Deserialize<object>(s.GetRawText()) : null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static object? SummarizeImprovement(object improve)
    {
        try
        {
            var el = System.Text.Json.JsonSerializer.SerializeToElement(improve);
            return new
            {
                viewKind = el.TryGetProperty("viewKind", out var v) ? v.GetString() : null,
                composite = el.TryGetProperty("composite", out var c)
                    ? System.Text.Json.JsonSerializer.Deserialize<object>(c.GetRawText())
                    : null,
                sparklineSource = el.TryGetProperty("sparklineSource", out var s) ? s.GetString() : null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<object?> Safe(Func<Task<object>> action)
    {
        try
        {
            return await action();
        }
        catch
        {
            return null;
        }
    }
}
