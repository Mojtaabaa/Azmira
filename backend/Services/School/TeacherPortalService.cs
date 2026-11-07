using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class TeacherPortalService(AppDbContext db, SchoolAccess access, OutboxPublisher outbox)
{
    public async Task<object> MyClassesAsync(Guid workspaceId, int teacherUserId, WorkspaceMemberRole role, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        IQueryable<ClassGroup> query = db.Classes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null);

        if (!SchoolAccess.IsSchoolStaff(role))
        {
            var classIds = await access.TeacherClassIdsAsync(workspaceId, teacherUserId, cancellationToken);
            query = query.Where(item => classIds.Contains(item.Id));
        }

        var items = await query.OrderBy(item => item.Name)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                academicYearId = item.AcademicYearId,
                gradeId = item.GradeId,
                studentCount = item.Members.Count(m => m.Role == ClassMemberRole.Student),
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> ClassStudentsAsync(Guid workspaceId, int teacherUserId, WorkspaceMemberRole role, Guid classId, CancellationToken cancellationToken)
    {
        await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherUserId, classId, role, cancellationToken);
        var students = await db.ClassMembers.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Role == ClassMemberRole.Student)
            .Join(db.Users.AsNoTracking(), m => m.UserId, u => u.Id, (m, u) => new
            {
                userId = m.UserId,
                displayName = u.DisplayName ?? u.Username,
                joinedAt = m.JoinedAt,
            })
            .ToListAsync(cancellationToken);
        return new { classId, items = students };
    }

    public async Task<object> StudentProgressAsync(
        Guid workspaceId,
        int teacherUserId,
        WorkspaceMemberRole role,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        await access.EnsureTeacherCanAccessStudentAsync(workspaceId, teacherUserId, role, studentUserId, cancellationToken);
        var stats = await db.UserGameStats.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId, cancellationToken);
        var mastery = await db.MasteryNodes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId)
            .OrderBy(item => item.ScoreEma)
            .Take(20)
            .Select(item => new
            {
                item.Subject,
                item.Chapter,
                item.Topic,
                level = item.Level.ToString(),
                item.ScoreEma,
            })
            .ToListAsync(cancellationToken);
        var recentAttempts = await db.AssessmentAttempts.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId && item.SubmittedAt != null)
            .OrderByDescending(item => item.SubmittedAt)
            .Take(10)
            .Select(item => new { item.Id, title = item.Assessment.Title, item.Percent, item.SubmittedAt })
            .ToListAsync(cancellationToken);

        return new
        {
            studentUserId,
            xpTotal = stats?.XpTotal ?? 0,
            level = stats?.Level ?? 1,
            streakCurrent = stats?.StreakCurrent ?? 0,
            mastery,
            recentAttempts,
        };
    }

    public async Task<object> CreateAssignmentAsync(
        Guid workspaceId,
        int teacherUserId,
        WorkspaceMemberRole role,
        Guid classId,
        Guid? subjectId,
        string title,
        string? description,
        DateTime? dueAt,
        bool publish,
        CancellationToken cancellationToken)
    {
        await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherUserId, classId, role, cancellationToken);
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان تکلیف لازم است.");
        if (subjectId is Guid sid && !await db.SchoolSubjects.AnyAsync(item => item.Id == sid && item.WorkspaceId == workspaceId, cancellationToken))
            throw new InvalidOperationException("درس نامعتبر است.");

        var now = DateTime.UtcNow;
        var assignment = new SchoolAssignment
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClassId = classId,
            SubjectId = subjectId,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            Status = publish ? AssignmentStatus.Published : AssignmentStatus.Draft,
            DueAt = dueAt,
            CreatedByUserId = teacherUserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SchoolAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = assignment.Id, status = assignment.Status.ToString() };
    }

    public async Task<object> ListAssignmentsAsync(Guid workspaceId, int teacherUserId, WorkspaceMemberRole role, Guid? classId, CancellationToken cancellationToken)
    {
        await access.EnsureSchoolWorkspaceAsync(workspaceId, cancellationToken);
        var query = db.SchoolAssignments.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
        if (!SchoolAccess.IsSchoolStaff(role))
        {
            var classIds = await access.TeacherClassIdsAsync(workspaceId, teacherUserId, cancellationToken);
            query = query.Where(item => classIds.Contains(item.ClassId));
        }

        if (classId is Guid cid)
        {
            await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherUserId, cid, role, cancellationToken);
            query = query.Where(item => item.ClassId == cid);
        }

        var items = await query.OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                classId = item.ClassId,
                subjectId = item.SubjectId,
                title = item.Title,
                status = item.Status.ToString(),
                dueAt = item.DueAt,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateMissionAsync(
        Guid workspaceId,
        int teacherUserId,
        WorkspaceMemberRole role,
        Guid classId,
        string title,
        string? description,
        int targetValue,
        int rewardXp,
        int rewardCoins,
        int? assignedUserId,
        CancellationToken cancellationToken)
    {
        await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherUserId, classId, role, cancellationToken);
        if (assignedUserId is int sid)
        {
            await access.EnsureTeacherCanAccessStudentAsync(workspaceId, teacherUserId, role, sid, cancellationToken);
        }

        var quest = new QuestDefinition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Title = title.Trim(),
            Description = description?.Trim() ?? $"ماموریت کلاسی {classId}",
            Category = "classroom",
            Kind = QuestKind.Special,
            TargetValue = Math.Max(1, targetValue),
            RewardXp = Math.Max(0, rewardXp),
            RewardCoins = Math.Max(0, rewardCoins),
            CompletionCondition = "manual",
            Visibility = QuestVisibility.Public,
            Difficulty = 2,
            Status = QuestStatus.Active,
            AssignedUserId = assignedUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Quests.Add(quest);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = quest.Id, title = quest.Title };
    }

    public async Task<object> MonitorActivityAsync(Guid workspaceId, int teacherUserId, WorkspaceMemberRole role, Guid classId, CancellationToken cancellationToken)
    {
        await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherUserId, classId, role, cancellationToken);
        var studentIds = await db.ClassMembers.AsNoTracking()
            .Where(item => item.ClassId == classId && item.Role == ClassMemberRole.Student)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);

        var weekStart = DateTime.UtcNow.Date.AddDays(-7);
        var xp = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && studentIds.Contains(item.UserId) && item.CreatedAt >= weekStart && item.Kind == LedgerKind.Earn)
            .GroupBy(item => item.UserId)
            .Select(g => new { userId = g.Key, xp = g.Sum(item => item.Delta) })
            .ToListAsync(cancellationToken);

        var streaks = await db.UserGameStats.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && studentIds.Contains(item.UserId))
            .Select(item => new { userId = item.UserId, streak = item.StreakCurrent })
            .ToListAsync(cancellationToken);

        return new { classId, weekXp = xp, streaks };
    }

    public async Task<object> AddFeedbackAsync(
        Guid workspaceId,
        int teacherUserId,
        WorkspaceMemberRole role,
        int studentUserId,
        Guid? classId,
        string body,
        CancellationToken cancellationToken)
    {
        await access.EnsureTeacherCanAccessStudentAsync(workspaceId, teacherUserId, role, studentUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(body)) throw new InvalidOperationException("متن بازخورد لازم است.");
        var row = new TeacherFeedback
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClassId = classId,
            TeacherUserId = teacherUserId,
            StudentUserId = studentUserId,
            Body = body.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.TeacherFeedback.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = row.Id };
    }

    public async Task<object> AwardBadgeAsync(
        Guid workspaceId,
        int teacherUserId,
        WorkspaceMemberRole role,
        int studentUserId,
        Guid badgeId,
        CancellationToken cancellationToken)
    {
        await access.EnsureTeacherCanAccessStudentAsync(workspaceId, teacherUserId, role, studentUserId, cancellationToken);
        var extras = await db.SchoolSettingsExtras.AsNoTracking().FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        if (extras is { AllowTeacherBadges: false } && !SchoolAccess.IsAdmin(role))
            throw new InvalidOperationException("اعطای نشان توسط معلم غیرفعال است.");

        var badge = await db.Badges.AsNoTracking().FirstOrDefaultAsync(
            item => item.Id == badgeId && (item.WorkspaceId == null || item.WorkspaceId == workspaceId),
            cancellationToken) ?? throw new InvalidOperationException("نشان پیدا نشد.");

        if (await db.UserBadges.AnyAsync(item => item.WorkspaceId == workspaceId && item.UserId == studentUserId && item.BadgeId == badgeId, cancellationToken))
            return new { ok = true, already = true };

        db.UserBadges.Add(new UserBadge
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = studentUserId,
            BadgeId = badgeId,
            AwardedAt = DateTime.UtcNow,
        });
        db.GameNotifications.Add(new GameNotification
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = studentUserId,
            Kind = GameNotificationKind.BadgeUnlocked,
            Title = "نشان جدید",
            Body = badge.Title,
            PayloadJson = "{}",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        _ = outbox;
        return new { ok = true, badgeId, title = badge.Title };
    }
}
