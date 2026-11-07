using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

public sealed class SpacedRepetitionService(AppDbContext db)
{
    public const string DefaultAlgorithm = "sm2-lite";

    public async Task EnsureDefaultProfileAsync(CancellationToken cancellationToken = default)
    {
        if (await db.SpacedRepetitionProfiles.AnyAsync(item => item.Code == "default" && item.IsSystem, cancellationToken))
            return;

        db.SpacedRepetitionProfiles.Add(new SpacedRepetitionProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = null,
            Code = "default",
            Name = "بازبینی پیش‌فرض",
            AlgorithmCode = DefaultAlgorithm,
            ConfigJson = JsonSerializer.Serialize(new
            {
                minEase = 1.3,
                startingEase = 2.5,
                graduatingIntervalDays = 1,
                easyBonus = 1.3,
            }),
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<object> EnsureCardAsync(
        Guid workspaceId,
        int userId,
        string subject,
        string? chapter,
        string topic,
        CancellationToken cancellationToken)
    {
        var existing = await db.SpacedRepetitionCards.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.Subject == subject
                && item.Topic == topic
                && item.Chapter == chapter,
            cancellationToken);
        if (existing is not null) return new { id = existing.Id, created = false };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var card = new SpacedRepetitionCard
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Subject = subject,
            Chapter = chapter,
            Topic = topic,
            AlgorithmCode = DefaultAlgorithm,
            AlgorithmStateJson = "{}",
            NextReviewDate = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.SpacedRepetitionCards.Add(card);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = card.Id, created = true };
    }

    public async Task<object> DueAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await db.SpacedRepetitionCards.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.NextReviewDate <= today)
            .OrderBy(item => item.NextReviewDate)
            .Select(item => new
            {
                id = item.Id,
                subject = item.Subject,
                chapter = item.Chapter,
                topic = item.Topic,
                nextReviewDate = item.NextReviewDate,
                overdueDays = today.DayNumber - item.NextReviewDate.DayNumber,
                difficulty = item.Difficulty,
                confidence = item.Confidence,
                intervalDays = item.IntervalDays,
                repetition = item.Repetition,
            })
            .ToListAsync(cancellationToken);
        return new { asOf = today, items };
    }

    public async Task<object> HistoryAsync(Guid workspaceId, int userId, Guid? cardId, CancellationToken cancellationToken)
    {
        var query = db.SpacedRepetitionReviews.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId);
        if (cardId is Guid id) query = query.Where(item => item.CardId == id);

        var items = await query.OrderByDescending(item => item.ReviewedAt).Take(100)
            .Select(item => new
            {
                id = item.Id,
                cardId = item.CardId,
                outcome = item.Outcome.ToString(),
                successful = item.Successful,
                difficultyRating = item.DifficultyRating,
                confidenceRating = item.ConfidenceRating,
                intervalBeforeDays = item.IntervalBeforeDays,
                intervalAfterDays = item.IntervalAfterDays,
                algorithmCode = item.AlgorithmCode,
                reviewedAt = item.ReviewedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> ReviewAsync(
        Guid workspaceId,
        int userId,
        Guid cardId,
        ReviewOutcome outcome,
        byte difficulty,
        byte confidence,
        MasteryService mastery,
        CancellationToken cancellationToken)
    {
        var card = await db.SpacedRepetitionCards.FirstOrDefaultAsync(
            item => item.Id == cardId && item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken) ?? throw new InvalidOperationException("کارت بازبینی پیدا نشد.");

        difficulty = Math.Clamp(difficulty, (byte)1, (byte)5);
        confidence = Math.Clamp(confidence, (byte)1, (byte)5);

        var before = Snapshot(card);
        var beforeJson = JsonSerializer.Serialize(before);
        var intervalBefore = card.IntervalDays;
        ApplySm2Lite(card, outcome, difficulty, confidence);
        var after = Snapshot(card);
        var afterJson = JsonSerializer.Serialize(after);

        var successful = outcome is ReviewOutcome.Good or ReviewOutcome.Easy;
        db.SpacedRepetitionReviews.Add(new SpacedRepetitionReview
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            WorkspaceId = workspaceId,
            UserId = userId,
            Outcome = outcome,
            DifficultyRating = difficulty,
            ConfidenceRating = confidence,
            Successful = successful,
            IntervalBeforeDays = intervalBefore,
            IntervalAfterDays = card.IntervalDays,
            AlgorithmCode = card.AlgorithmCode,
            StateBeforeJson = beforeJson,
            StateAfterJson = afterJson,
            ReviewedAt = DateTime.UtcNow,
        });

        card.LastReviewedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await mastery.ApplyReviewAsync(workspaceId, userId, card.Subject, card.Chapter, card.Topic, successful, cancellationToken);

        return new
        {
            id = card.Id,
            nextReviewDate = card.NextReviewDate,
            intervalDays = card.IntervalDays,
            easeFactor = card.EaseFactor,
            repetition = card.Repetition,
            successful,
        };
    }

    private static void ApplySm2Lite(SpacedRepetitionCard card, ReviewOutcome outcome, byte difficulty, byte confidence)
    {
        // Historical reviews keep StateBefore/After so algorithm code can evolve safely.
        var ease = card.EaseFactor <= 0 ? 2.5 : card.EaseFactor;
        var q = outcome switch
        {
            ReviewOutcome.Fail => 1,
            ReviewOutcome.Hard => 3,
            ReviewOutcome.Good => 4,
            ReviewOutcome.Easy => 5,
            _ => 3,
        };
        q = Math.Clamp(q - (difficulty >= 4 ? 1 : 0) + (confidence >= 4 ? 1 : 0), 0, 5);

        ease = Math.Max(1.3, ease + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02)));
        card.EaseFactor = Math.Round(ease, 3);
        card.Difficulty = difficulty;
        card.Confidence = confidence;

        if (outcome == ReviewOutcome.Fail)
        {
            card.Repetition = 0;
            card.IntervalDays = 1;
        }
        else
        {
            card.Repetition += 1;
            card.IntervalDays = card.Repetition switch
            {
                1 => 1,
                2 => outcome == ReviewOutcome.Easy ? 4 : 3,
                _ => Math.Max(1, (int)Math.Round(card.IntervalDays * card.EaseFactor * (outcome == ReviewOutcome.Easy ? 1.3 : 1.0))),
            };
        }

        card.NextReviewDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(card.IntervalDays));
        card.AlgorithmStateJson = JsonSerializer.Serialize(new
        {
            ease = card.EaseFactor,
            repetition = card.Repetition,
            intervalDays = card.IntervalDays,
        });
    }

    private static object Snapshot(SpacedRepetitionCard card) => new
    {
        card.EaseFactor,
        card.Repetition,
        card.IntervalDays,
        card.NextReviewDate,
        card.Difficulty,
        card.Confidence,
        card.AlgorithmCode,
    };
}
