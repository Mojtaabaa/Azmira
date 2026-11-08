using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Schools;

namespace KonkoorHelp.Api.Services;

public static class UserCapabilities
{
    public static object Build(byte platformRole, IReadOnlyList<WorkspaceListItem> workspaces, Guid? activeWorkspaceId)
    {
        var platformAdmin = PlatformRoles.IsAdministrator(platformRole);
        var schoolAdmin = workspaces.Any(item =>
            item.Type == WorkspaceType.School && SchoolAccess.IsAdmin(item.Role));
        var teacher = workspaces.Any(item =>
            item.Type == WorkspaceType.School && item.Role == WorkspaceMemberRole.Teacher);
        var teacherOrAbove = workspaces.Any(item =>
            item.Type == WorkspaceType.School && SchoolAccess.IsTeacherOrAbove(item.Role));
        var parent = workspaces.Any(item =>
            item.Type == WorkspaceType.School && item.Role == WorkspaceMemberRole.Parent);
        var schoolMember = workspaces.Any(item => item.Type == WorkspaceType.School);

        var active = activeWorkspaceId is Guid id
            ? workspaces.FirstOrDefault(item => item.Id == id)
            : null;
        var activeIsSchool = active?.Type == WorkspaceType.School;
        var activeRole = active?.Role ?? WorkspaceMemberRole.Student;

        return new
        {
            platformAdmin,
            schoolAdmin,
            teacher = teacher || schoolAdmin,
            parent,
            schoolMember,
            teacherOrAbove = teacherOrAbove || platformAdmin,
            canRequestSchool = true,
            canManageSchool = schoolAdmin || platformAdmin,
            active = new
            {
                schoolAdmin = activeIsSchool && (SchoolAccess.IsAdmin(activeRole) || platformAdmin),
                teacher = activeIsSchool && (activeRole == WorkspaceMemberRole.Teacher || SchoolAccess.IsAdmin(activeRole) || platformAdmin),
                parent = activeIsSchool && activeRole == WorkspaceMemberRole.Parent,
                teacherOrAbove = activeIsSchool && (SchoolAccess.IsTeacherOrAbove(activeRole) || platformAdmin),
                canManageSchool = activeIsSchool && (SchoolAccess.IsAdmin(activeRole) || platformAdmin),
                isSchool = activeIsSchool,
            },
        };
    }
}
