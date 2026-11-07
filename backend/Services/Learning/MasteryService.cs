using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

public sealed class MasteryService(AppDbContext db, OutboxPublisher outbox)
{
    private const double EmaAlpha = 0.35;

    public async Task<object> GetMapAsync(Guid workspaceId, int userId, string? subject, CancellationToken cancellationToken)
    {
        var query = db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var s = subject.Trim();
            query = query.Where(item => item.Subject == s);
        }

        var nodes = await query.OrderBy(item => item.Subject).ThenBy(item => item.Chapter).ThenBy(item => item.Topic)
            .Select(item => new
            {
                id = item.Id,
                subject = item.Subject,
                chapter = item.Chapter,
                topic = item.Topic,
                level = item.Level.ToString(),
                scoreEma = item.ScoreEma,
                evidenceCount = item.EvidenceCount,
                correctCount = item.CorrectCount,
                reviewSuccessCount = item.ReviewSuccessCount,
                reviewFailCount = item.ReviewFailCount,
                lastEvidenceAt = item.LastEvidenceAt,
                updatedAt = item.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new { items = nodes };
    }

    public async Task ApplyEvidenceAsync(
        Guid workspaceId,
        int userId,
        string subject,
        string? chapter,
        string? topic,
        bool correct,
        double pointsAwarded,
        double pointsPossible,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject)) return;
        // Study duration alone must never call this path.

        var ratio = pointsPossible <= 0 ? (correct ? 1.0 : 0.0) : Math.Clamp(pointsAwarded / pointsPossible, 0, 1);
        await UpsertAsync(workspaceId, userId, subject.Trim(), null, null, ratio, correct, isReview: false, cancellationToken);
        if (!string.IsNullOrWhiteSpace(chapter))
        {
            await UpsertAsync(workspaceId, userId, subject.Trim(), chapter.Trim(), null, ratio, correct, isReview: false, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            await UpsertAsync(workspaceId, userId, subject.Trim(), chapter?.Trim(), topic.Trim(), ratio, correct, isReview: false, cancellationToken);
        }
    }

    public async Task ApplyReviewAsync(
        Guid workspaceId,
        int userId,
        string subject,
        string? chapter,
        string topic,
        bool successful,
        CancellationToken cancellationToken)
    {
        var ratio = successful ? 0.85 : 0.25;
        await UpsertAsync(workspaceId, userId, subject, chapter, topic, ratio, successful, isReview: true, cancellationToken);
    }

    private async Task UpsertAsync(
        Guid workspaceId,
        int userId,
        string subject,
        string? chapter,
        string? topic,
        double ratio,
        bool positive,
        bool isReview,
        CancellationToken cancellationToken)
    {
        var node = await db.MasteryNodes.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.Subject == subject
                && item.Chapter == chapter
                && item.Topic == topic,
            cancellationToken);

        var previous = node?.Level ?? MasteryLevel.NotStarted;
        if (node is null)
        {
            node = new MasteryNode
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Subject = subject,
                Chapter = chapter,
                Topic = topic,
                Level = MasteryLevel.Learning,
                ScoreEma = ratio,
                EvidenceCount = 0,
                UpdatedAt = DateTime.UtcNow,
            };
            db.MasteryNodes.Add(node);
        }

        node.EvidenceCount += 1;
        if (positive) node.CorrectCount += 1;
        if (isReview)
        {
            if (positive) node.ReviewSuccessCount += 1;
            else node.ReviewFailCount += 1;
        }

        node.ScoreEma = node.EvidenceCount == 1
            ? ratio
            : (EmaAlpha * ratio) + ((1 - EmaAlpha) * node.ScoreEma);
        node.LastEvidenceAt = DateTime.UtcNow;
        node.Level = ComputeLevel(node);
        node.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (node.Level > previous && !string.IsNullOrWhiteSpace(topic))
        {
            await outbox.PublishAsync(
                workspaceId,
                userId,
                DomainEventTypes.MasteryImproved,
                $"mastery:{userId}:{subject}:{topic}:{node.Level}",
                new { subject, chapter, topic, level = node.Level.ToString(), scoreEma = node.ScoreEma },
                cancellationToken);
        }
    }

    public static MasteryLevel ComputeLevel(MasteryNode node)
    {
        if (node.EvidenceCount <= 0) return MasteryLevel.NotStarted;

        var daysSince = node.LastEvidenceAt is DateTime at
            ? (DateTime.UtcNow - at).TotalDays
            : 999;
        var reviewPressure = node.ReviewFailCount > node.ReviewSuccessCount;

        if (daysSince > 21 && node.ScoreEma < 0.85) return MasteryLevel.NeedsReview;
        if (reviewPressure && node.ScoreEma < 0.7) return MasteryLevel.NeedsReview;
        if (node.EvidenceCount < 2) return MasteryLevel.Learning;
        if (node.ScoreEma >= 0.9 && node.EvidenceCount >= 5 && node.ReviewSuccessCount >= 2 && daysSince <= 14)
            return MasteryLevel.Mastered;
        if (node.ScoreEma >= 0.8 && node.EvidenceCount >= 4) return MasteryLevel.Proficient;
        if (node.ScoreEma >= 0.6) return MasteryLevel.Developing;
        if (node.ScoreEma >= 0.4) return MasteryLevel.Learning;
        return MasteryLevel.NeedsReview;
    }
}
