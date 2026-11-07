using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class SchoolAnalyticsService(AppDbContext db, SchoolAccess access)
{
    public async Task<object> DashboardAsync(
        Guid workspaceId,
        WorkspaceMemberRole role,
        int actorUserId,
        Guid? academicYearId,
        Guid? gradeId,
        Guid? classId,
        string? subject,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (!SchoolAccess.IsTeacherOrAbove(role))
            throw new UnauthorizedAccessException("دسترسی به تحلیل مدرسه نداری.");

        var start = from ?? DateTime.UtcNow.Date.AddDays(-30);
        var end = to ?? DateTime.UtcNow.AddDays(1);

        var classQuery = db.Classes.AsNoTracking().Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null);
        if (academicYearId is Guid y) classQuery = classQuery.Where(item => item.AcademicYearId == y);
        if (gradeId is Guid g) classQuery = classQuery.Where(item => item.GradeId == g);
        if (classId is Guid c)
        {
            await access.EnsureTeacherOwnsClassAsync(workspaceId, actorUserId, c, role, cancellationToken);
            classQuery = classQuery.Where(item => item.Id == c);
        }
        else if (role == WorkspaceMemberRole.Teacher)
        {
            var allowed = await access.TeacherClassIdsAsync(workspaceId, actorUserId, cancellationToken);
            // Materialize filter list for providers that mishandle open Contains.
            var allowedList = allowed.ToList();
            classQuery = classQuery.Where(item => allowedList.Contains(item.Id));
        }

        var classes = await classQuery.Select(item => new { item.Id, item.Name, item.GradeId }).ToListAsync(cancellationToken);
        var classIds = classes.Select(item => item.Id).ToList();
        var studentIds = await db.ClassMembers.AsNoTracking()
            .Where(item => classIds.Contains(item.ClassId) && item.Role == ClassMemberRole.Student)
            .Select(item => item.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var xpByUser = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && studentIds.Contains(item.UserId)
                && item.Kind == LedgerKind.Earn
                && item.CreatedAt >= start
                && item.CreatedAt < end)
            .GroupBy(item => item.UserId)
            .Select(g => new { userId = g.Key, xp = g.Sum(i => i.Delta) })
            .ToListAsync(cancellationToken);

        var streakActive = await db.UserGameStats.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId && studentIds.Contains(item.UserId) && item.StreakCurrent > 0, cancellationToken);

        var goalHits = await db.DomainOutbox.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId
                && item.UserId != null
                && studentIds.Contains(item.UserId.Value)
                && item.EventType == DomainEventTypes.StudyGoalCompleted
                && item.OccurredAt >= start
                && item.OccurredAt < end, cancellationToken);

        var attemptsQuery = db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && studentIds.Contains(item.UserId)
                && item.SubmittedAt != null
                && item.SubmittedAt >= start
                && item.SubmittedAt < end);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var sub = subject.Trim();
            attemptsQuery = attemptsQuery.Where(item => item.Assessment.Subject == sub);
        }

        var attempts = await attemptsQuery
            .Select(item => new { item.Percent, item.Assessment.Subject, item.UserId })
            .ToListAsync(cancellationToken);

        var masteryQuery = db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && studentIds.Contains(item.UserId) && item.Topic != null);
        if (!string.IsNullOrWhiteSpace(subject))
        {
            var sub = subject.Trim();
            masteryQuery = masteryQuery.Where(item => item.Subject == sub);
        }

        var masteryAvg = await masteryQuery
            .GroupBy(item => item.Subject)
            .Select(g => new { subject = g.Key, avgEma = g.Average(i => i.ScoreEma), count = g.Count() })
            .ToListAsync(cancellationToken);

        var classPerformance = new List<object>();
        foreach (var cls in classes)
        {
            var ids = await db.ClassMembers.AsNoTracking()
                .Where(item => item.ClassId == cls.Id && item.Role == ClassMemberRole.Student)
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            var avgXp = xpByUser.Where(item => ids.Contains(item.userId)).Select(item => item.xp).DefaultIfEmpty(0).Average();
            var avgScore = attempts.Where(item => ids.Contains(item.UserId)).Select(item => item.Percent).DefaultIfEmpty(0).Average();
            classPerformance.Add(new { classId = cls.Id, name = cls.Name, avgXp, avgAssessmentPercent = Math.Round(avgScore, 1) });
        }

        var gradePerformance = await db.GradeLevels.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);

        var gradeRows = new List<object>();
        foreach (var grade in gradePerformance)
        {
            var gradeClassIds = classes.Where(item => item.GradeId == grade.Id).Select(item => item.Id).ToList();
            var ids = await db.ClassMembers.AsNoTracking()
                .Where(item => gradeClassIds.Contains(item.ClassId) && item.Role == ClassMemberRole.Student)
                .Select(item => item.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            gradeRows.Add(new
            {
                gradeId = grade.Id,
                name = grade.Name,
                students = ids.Count,
                avgXp = xpByUser.Where(item => ids.Contains(item.userId)).Select(item => item.xp).DefaultIfEmpty(0).Average(),
            });
        }

        return new
        {
            filters = new { academicYearId, gradeId, classId, subject, from = start, to = end },
            studentActivity = new
            {
                activeStudents = studentIds.Count,
                studentsWithXp = xpByUser.Count,
                engagementRate = studentIds.Count == 0 ? 0 : Math.Round(100.0 * xpByUser.Count / studentIds.Count, 1),
                streakActive,
            },
            studyHoursProxy = new
            {
                note = "ساعت مطالعه از XP/استریک تقریبی است؛ جلسات خام در StudyRecords نیز قابل اتصال است.",
                totalXp = xpByUser.Sum(item => item.xp),
            },
            goalCompletion = new { hits = goalHits },
            assessmentPerformance = new
            {
                attemptCount = attempts.Count,
                averagePercent = attempts.Count == 0 ? 0 : Math.Round(attempts.Average(item => item.Percent), 1),
                bySubject = attempts.GroupBy(item => item.Subject)
                    .Select(g => new { subject = g.Key, averagePercent = Math.Round(g.Average(i => i.Percent), 1), count = g.Count() }),
            },
            learningImprovement = masteryAvg,
            subjectMastery = masteryAvg,
            classPerformance,
            gradePerformance = gradeRows,
        };
    }
}
