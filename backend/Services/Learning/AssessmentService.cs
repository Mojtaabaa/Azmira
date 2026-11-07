using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning;

public sealed class AssessmentService(AppDbContext db, MasteryService mastery, SpacedRepetitionService srs, OutboxPublisher outbox)
{
    public async Task<object> ListAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var items = await db.Assessments.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && (item.Status == AssessmentStatus.Published
                    || item.CreatedByUserId == userId
                    || item.Kind == AssessmentKind.SelfAssessment))
            .OrderByDescending(item => item.UpdatedAt)
            .Select(item => new
            {
                id = item.Id,
                title = item.Title,
                kind = item.Kind.ToString(),
                status = item.Status.ToString(),
                subject = item.Subject,
                chapter = item.Chapter,
                topic = item.Topic,
                classId = item.ClassId,
                passScore = item.PassScore,
                questionCount = item.Questions.Count,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> GetAsync(Guid workspaceId, Guid id, bool includeAnswers, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.AsNoTracking()
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == id && item.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("آزمون پیدا نشد.");

        return new
        {
            id = assessment.Id,
            title = assessment.Title,
            description = assessment.Description,
            kind = assessment.Kind.ToString(),
            status = assessment.Status.ToString(),
            subject = assessment.Subject,
            chapter = assessment.Chapter,
            topic = assessment.Topic,
            classId = assessment.ClassId,
            timeLimitMinutes = assessment.TimeLimitMinutes,
            passScore = assessment.PassScore,
            questions = assessment.Questions.OrderBy(q => q.Ordinal).Select(q => new
            {
                id = q.Id,
                ordinal = q.Ordinal,
                type = q.Type.ToString(),
                prompt = q.Prompt,
                options = JsonSerializer.Deserialize<object>(q.OptionsJson),
                points = q.Points,
                subject = q.Subject,
                chapter = q.Chapter,
                topic = q.Topic,
                correctAnswer = includeAnswers ? JsonSerializer.Deserialize<object>(q.CorrectAnswerJson) : null,
            }),
        };
    }

    public async Task<object> CreateAsync(
        Guid workspaceId,
        int userId,
        string title,
        string? description,
        AssessmentKind kind,
        string subject,
        string? chapter,
        string? topic,
        Guid? classId,
        double passScore,
        int? timeLimitMinutes,
        IReadOnlyList<QuestionInput> questions,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان لازم است.");
        if (string.IsNullOrWhiteSpace(subject)) throw new InvalidOperationException("موضوع لازم است.");
        if (questions.Count == 0) throw new InvalidOperationException("حداقل یک سؤال لازم است.");

        var now = DateTime.UtcNow;
        var assessment = new AssessmentDefinition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            Kind = kind,
            Status = publish ? AssessmentStatus.Published : AssessmentStatus.Draft,
            Subject = subject.Trim(),
            Chapter = string.IsNullOrWhiteSpace(chapter) ? null : chapter.Trim(),
            Topic = string.IsNullOrWhiteSpace(topic) ? null : topic.Trim(),
            ClassId = classId,
            CreatedByUserId = userId,
            TimeLimitMinutes = timeLimitMinutes,
            PassScore = Math.Clamp(passScore, 0, 100),
            CreatedAt = now,
            UpdatedAt = now,
        };

        var ordinal = 1;
        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q.Prompt)) throw new InvalidOperationException("متن سؤال خالی است.");
            assessment.Questions.Add(new AssessmentQuestion
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                Ordinal = ordinal++,
                Type = q.Type,
                Prompt = q.Prompt.Trim(),
                OptionsJson = q.OptionsJson ?? "[]",
                CorrectAnswerJson = q.CorrectAnswerJson ?? "null",
                Points = q.Points <= 0 ? 1 : q.Points,
                Subject = string.IsNullOrWhiteSpace(q.Subject) ? assessment.Subject : q.Subject.Trim(),
                Chapter = string.IsNullOrWhiteSpace(q.Chapter) ? assessment.Chapter : q.Chapter.Trim(),
                Topic = string.IsNullOrWhiteSpace(q.Topic) ? assessment.Topic : q.Topic.Trim(),
            });
        }

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = assessment.Id, status = assessment.Status.ToString() };
    }

    public async Task<object> StartAttemptAsync(Guid workspaceId, int userId, Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assessmentId && item.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("آزمون پیدا نشد.");
        if (assessment.Status != AssessmentStatus.Published && assessment.CreatedByUserId != userId)
            throw new InvalidOperationException("آزمون منتشر نشده است.");

        var attempt = new AssessmentAttempt
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            AssessmentId = assessmentId,
            UserId = userId,
            Status = AttemptStatus.InProgress,
            StartedAt = DateTime.UtcNow,
        };
        db.AssessmentAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);
        return new { attemptId = attempt.Id, startedAt = attempt.StartedAt };
    }

    public async Task<object> SubmitAttemptAsync(
        Guid workspaceId,
        int userId,
        Guid attemptId,
        IReadOnlyList<AnswerInput> answers,
        CancellationToken cancellationToken)
    {
        var attempt = await db.AssessmentAttempts
            .Include(item => item.Answers)
            .FirstOrDefaultAsync(item => item.Id == attemptId && item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("تلاش پیدا نشد.");
        if (attempt.Status != AttemptStatus.InProgress)
            throw new InvalidOperationException("این تلاش قبلاً ارسال شده.");

        var questions = await db.AssessmentQuestions
            .Where(item => item.AssessmentId == attempt.AssessmentId)
            .ToListAsync(cancellationToken);
        var byId = questions.ToDictionary(item => item.Id);

        double earned = 0;
        double max = questions.Sum(item => item.Points);
        foreach (var answer in answers)
        {
            if (!byId.TryGetValue(answer.QuestionId, out var question)) continue;
            var (correct, points) = ScoreAnswer(question, answer.ResponseJson);
            earned += points;
            db.AssessmentAnswers.Add(new AssessmentAnswer
            {
                Id = Guid.NewGuid(),
                AttemptId = attempt.Id,
                QuestionId = question.Id,
                ResponseJson = answer.ResponseJson ?? "null",
                IsCorrect = correct,
                PointsAwarded = points,
                DurationSeconds = answer.DurationSeconds,
            });

            await mastery.ApplyEvidenceAsync(
                workspaceId,
                userId,
                question.Subject,
                question.Chapter,
                question.Topic,
                correct == true,
                points,
                question.Points,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(question.Topic))
            {
                await srs.EnsureCardAsync(workspaceId, userId, question.Subject, question.Chapter, question.Topic!, cancellationToken);
            }
        }

        attempt.Status = AttemptStatus.Scored;
        attempt.SubmittedAt = DateTime.UtcNow;
        attempt.DurationSeconds = Math.Max(0, (int)(attempt.SubmittedAt.Value - attempt.StartedAt).TotalSeconds);
        attempt.Score = earned;
        attempt.MaxScore = max;
        attempt.Percent = max <= 0 ? 0 : Math.Round(100.0 * earned / max, 2);
        await db.SaveChangesAsync(cancellationToken);

        var assessment = await db.Assessments.AsNoTracking().FirstAsync(item => item.Id == attempt.AssessmentId, cancellationToken);
        await outbox.PublishAsync(
            workspaceId,
            userId,
            DomainEventTypes.AssessmentSubmitted,
            $"assessment:{attempt.Id}:submitted",
            new { attemptId = attempt.Id, subjectId = assessment.Subject, score = attempt.Percent },
            cancellationToken);

        if (attempt.Percent >= assessment.PassScore)
        {
            await outbox.PublishAsync(
                workspaceId,
                userId,
                DomainEventTypes.AssessmentPassed,
                $"assessment:{attempt.Id}:passed",
                new { attemptId = attempt.Id, subjectId = assessment.Subject, score = attempt.Percent },
                cancellationToken);
        }

        return new
        {
            attemptId = attempt.Id,
            score = attempt.Score,
            maxScore = attempt.MaxScore,
            percent = attempt.Percent,
            durationSeconds = attempt.DurationSeconds,
            passed = attempt.Percent >= assessment.PassScore,
        };
    }

    public async Task<object> AttemptHistoryAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var items = await db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Status != AttemptStatus.InProgress)
            .OrderByDescending(item => item.SubmittedAt)
            .Take(50)
            .Select(item => new
            {
                id = item.Id,
                assessmentId = item.AssessmentId,
                title = item.Assessment.Title,
                percent = item.Percent,
                score = item.Score,
                maxScore = item.MaxScore,
                durationSeconds = item.DurationSeconds,
                submittedAt = item.SubmittedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    private static (bool? Correct, double Points) ScoreAnswer(AssessmentQuestion question, string? responseJson)
    {
        try
        {
            using var response = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseJson) ? "null" : responseJson);
            using var correct = JsonDocument.Parse(question.CorrectAnswerJson);

            if (question.Type == QuestionType.Descriptive)
            {
                // Manual/self: award full points if non-empty response; mark null correctness.
                var text = response.RootElement.ValueKind == JsonValueKind.String
                    ? response.RootElement.GetString()
                    : response.RootElement.ToString();
                var hasText = !string.IsNullOrWhiteSpace(text) && text != "null";
                return (null, hasText ? question.Points * 0.5 : 0);
            }

            var match = JsonEqual(response.RootElement, correct.RootElement);
            return (match, match ? question.Points : 0);
        }
        catch
        {
            return (false, 0);
        }
    }

    private static bool JsonEqual(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind) return false;
        return a.ValueKind switch
        {
            JsonValueKind.String => string.Equals(a.GetString(), b.GetString(), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => a.GetDouble().Equals(b.GetDouble()),
            JsonValueKind.True or JsonValueKind.False => a.GetBoolean() == b.GetBoolean(),
            JsonValueKind.Null => true,
            _ => a.GetRawText().Equals(b.GetRawText(), StringComparison.OrdinalIgnoreCase),
        };
    }
}

public sealed record QuestionInput(
    QuestionType Type,
    string Prompt,
    string? OptionsJson,
    string? CorrectAnswerJson,
    double Points,
    string? Subject,
    string? Chapter,
    string? Topic);

public sealed record AnswerInput(Guid QuestionId, string? ResponseJson, int? DurationSeconds);
