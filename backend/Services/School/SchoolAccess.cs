using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Schools;

public sealed class SchoolAccess(AppDbContext db)
{
    public static bool IsSchoolStaff(WorkspaceMemberRole role) =>
        role is WorkspaceMemberRole.SchoolOwner
            or WorkspaceMemberRole.SchoolAdministrator
            or WorkspaceMemberRole.AcademicManager;

    public static bool IsAdmin(WorkspaceMemberRole role) =>
        role is WorkspaceMemberRole.SchoolOwner or WorkspaceMemberRole.SchoolAdministrator;

    public static bool IsTeacherOrAbove(WorkspaceMemberRole role) =>
        IsSchoolStaff(role) || role == WorkspaceMemberRole.Teacher;

    public async Task EnsureSchoolWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var type = await db.Workspaces.AsNoTracking()
            .Where(item => item.Id == workspaceId && item.DeletedAt == null)
            .Select(item => (WorkspaceType?)item.Type)
            .FirstOrDefaultAsync(cancellationToken);
        if (type != WorkspaceType.School)
            throw new InvalidOperationException("این عملیات فقط در فضای مدرسه مجاز است.");
    }

    public async Task<HashSet<Guid>> TeacherClassIdsAsync(Guid workspaceId, int teacherUserId, CancellationToken cancellationToken)
    {
        var ids = await (
            from member in db.ClassMembers.AsNoTracking()
            join cls in db.Classes.AsNoTracking() on member.ClassId equals cls.Id
            where member.UserId == teacherUserId
                && member.Role == ClassMemberRole.Teacher
                && cls.WorkspaceId == workspaceId
                && cls.DeletedAt == null
            select member.ClassId).ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public async Task EnsureTeacherOwnsClassAsync(Guid workspaceId, int teacherUserId, Guid classId, WorkspaceMemberRole role, CancellationToken cancellationToken)
    {
        var classOk = await db.Classes.AsNoTracking()
            .AnyAsync(item => item.Id == classId && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken);
        if (!classOk) throw new UnauthorizedAccessException("کلاس پیدا نشد.");

        if (IsSchoolStaff(role)) return;

        if (role != WorkspaceMemberRole.Teacher)
            throw new UnauthorizedAccessException("فقط معلم یا مدیر مدرسه مجاز است.");

        var ok = await (
            from member in db.ClassMembers.AsNoTracking()
            join cls in db.Classes.AsNoTracking() on member.ClassId equals cls.Id
            where member.ClassId == classId
                && member.UserId == teacherUserId
                && member.Role == ClassMemberRole.Teacher
                && cls.WorkspaceId == workspaceId
                && cls.DeletedAt == null
            select member.Id).AnyAsync(cancellationToken);
        if (!ok) throw new UnauthorizedAccessException("به این کلاس دسترسی نداری.");
    }

    public async Task EnsureTeacherCanAccessStudentAsync(
        Guid workspaceId,
        int actorUserId,
        WorkspaceMemberRole role,
        int studentUserId,
        CancellationToken cancellationToken)
    {
        if (IsSchoolStaff(role))
        {
            var member = await db.WorkspaceMembers.AsNoTracking().AnyAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == studentUserId && item.Status == WorkspaceMemberStatus.Active,
                cancellationToken);
            if (!member) throw new UnauthorizedAccessException("دانش‌آموز عضو این مدرسه نیست.");
            return;
        }

        if (role != WorkspaceMemberRole.Teacher)
            throw new UnauthorizedAccessException("دسترسی مجاز نیست.");

        // Join-based check (avoid local-collection Contains — unreliable on EF InMemory).
        var shared = await (
            from studentMembership in db.ClassMembers.AsNoTracking()
            join teacherMembership in db.ClassMembers.AsNoTracking()
                on studentMembership.ClassId equals teacherMembership.ClassId
            join cls in db.Classes.AsNoTracking() on studentMembership.ClassId equals cls.Id
            where teacherMembership.UserId == actorUserId
                && teacherMembership.Role == ClassMemberRole.Teacher
                && studentMembership.UserId == studentUserId
                && studentMembership.Role == ClassMemberRole.Student
                && cls.WorkspaceId == workspaceId
                && cls.DeletedAt == null
            select studentMembership.Id).AnyAsync(cancellationToken);
        if (!shared) throw new UnauthorizedAccessException("این دانش‌آموز در کلاس‌های تو نیست.");
    }

    public async Task EnsureParentOfAsync(Guid workspaceId, int parentUserId, int studentUserId, CancellationToken cancellationToken)
    {
        var ok = await db.GuardianStudentLinks.AsNoTracking().AnyAsync(
            item => item.WorkspaceId == workspaceId
                && item.ParentUserId == parentUserId
                && item.StudentUserId == studentUserId
                && item.Status == GuardianLinkStatus.Verified,
            cancellationToken);
        if (!ok) throw new UnauthorizedAccessException("ارتباط والد-فرزند تأییدشده پیدا نشد.");
    }
}
