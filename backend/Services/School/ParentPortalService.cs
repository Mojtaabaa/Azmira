using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class ParentPortalService(AppDbContext db, SchoolAccess access)
{
    public async Task<object> RequestLinkAsync(Guid workspaceId, int parentUserId, int studentUserId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        if (parentUserId == studentUserId) throw new InvalidOperationException("والد و دانش‌آموز نمی‌توانند یک نفر باشند.");

        var studentOk = await db.WorkspaceMembers.AnyAsync(
            item => item.WorkspaceId == workspaceId
                && item.UserId == studentUserId
                && item.Role == WorkspaceMemberRole.Student
                && item.Status == WorkspaceMemberStatus.Active,
            cancellationToken);
        if (!studentOk) throw new InvalidOperationException("دانش‌آموز عضو این مدرسه نیست.");

        var existing = await db.GuardianStudentLinks.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.ParentUserId == parentUserId && item.StudentUserId == studentUserId,
            cancellationToken);
        if (existing is not null)
            return new { id = existing.Id, status = existing.Status.ToString() };

        // Ensure parent membership exists
        if (!await db.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == parentUserId && item.DeletedAt == null, cancellationToken))
        {
            var now = DateTime.UtcNow;
            db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = parentUserId,
                Role = WorkspaceMemberRole.Parent,
                Status = WorkspaceMemberStatus.Active,
                JoinedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        var link = new GuardianStudentLink
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ParentUserId = parentUserId,
            StudentUserId = studentUserId,
            Status = GuardianLinkStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.GuardianStudentLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = link.Id, status = link.Status.ToString() };
    }

    public async Task<object> VerifyLinkAsync(Guid workspaceId, int actorUserId, Guid linkId, bool approve, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var link = await db.GuardianStudentLinks.FirstOrDefaultAsync(item => item.Id == linkId && item.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("ارتباط پیدا نشد.");

        link.Status = approve ? GuardianLinkStatus.Verified : GuardianLinkStatus.Revoked;
        link.VerifiedByUserId = actorUserId;
        link.VerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new { id = link.Id, status = link.Status.ToString() };
    }

    public async Task<object> ListChildrenAsync(Guid workspaceId, int parentUserId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var items = await db.GuardianStudentLinks.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && item.ParentUserId == parentUserId
                && item.Status == GuardianLinkStatus.Verified)
            .Join(db.Users.AsNoTracking(), l => l.StudentUserId, u => u.Id, (l, u) => new
            {
                studentUserId = l.StudentUserId,
                displayName = u.DisplayName ?? u.Username,
                verifiedAt = l.VerifiedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> ChildSummaryAsync(Guid workspaceId, int parentUserId, int studentUserId, CancellationToken cancellationToken)
    {
        await access.EnsureParentOfAsync(workspaceId, parentUserId, studentUserId, cancellationToken);
        var extras = await db.SchoolSettingsExtras.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        if (extras is { AllowParentReadOnly: false })
            throw new UnauthorizedAccessException("دسترسی والدین در این مدرسه غیرفعال است.");

        var weekStart = DateTime.UtcNow.Date.AddDays(-((int)DateTime.UtcNow.DayOfWeek + 6) % 7);
        var stats = await db.UserGameStats.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId, cancellationToken);

        var studyDays = await db.StreakDays.AsNoTracking()
            .CountAsync(item => item.WorkspaceId == workspaceId
                && item.UserId == studentUserId
                && item.LocalDate >= DateOnly.FromDateTime(weekStart)
                && item.Kind == StreakDayKind.Study, cancellationToken);

        var assignments = await db.SchoolAssignments.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.Status == AssignmentStatus.Published)
            .Where(item => db.ClassMembers.Any(m => m.ClassId == item.ClassId && m.UserId == studentUserId && m.Role == ClassMemberRole.Student))
            .Select(item => new
            {
                id = item.Id,
                title = item.Title,
                dueAt = item.DueAt,
                status = db.SchoolAssignmentSubmissions
                    .Where(s => s.AssignmentId == item.Id && s.StudentUserId == studentUserId)
                    .Select(s => s.Status.ToString())
                    .FirstOrDefault() ?? AssignmentSubmissionStatus.NotStarted.ToString(),
            })
            .Take(20)
            .ToListAsync(cancellationToken);

        var assessments = await db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId && item.SubmittedAt != null)
            .OrderByDescending(item => item.SubmittedAt)
            .Take(10)
            .Select(item => new { title = item.Assessment.Title, item.Percent, item.SubmittedAt })
            .ToListAsync(cancellationToken);

        var badges = await db.UserBadges.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId)
            .OrderByDescending(item => item.AwardedAt)
            .Take(10)
            .Select(item => new { title = item.Badge.Title, item.AwardedAt })
            .ToListAsync(cancellationToken);

        return new
        {
            studentUserId,
            readOnly = true,
            studySummary = new
            {
                xpTotal = stats?.XpTotal ?? 0,
                level = stats?.Level ?? 1,
                streakCurrent = stats?.StreakCurrent ?? 0,
                studyDaysThisWeek = studyDays,
            },
            weeklyProgress = new { studyDays, weekStart },
            assignments,
            assessments,
            achievements = badges,
            consistency = new { streakCurrent = stats?.StreakCurrent ?? 0, streakBest = stats?.StreakBest ?? 0 },
            upcomingGoals = await db.UserQuestProgress.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId && item.Status == QuestStatus.Active)
                .Select(item => new { title = item.Quest.Title, item.Progress, target = item.Quest.TargetValue })
                .Take(10)
                .ToListAsync(cancellationToken),
        };
    }
}
