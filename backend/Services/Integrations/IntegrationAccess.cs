using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Integrations;

public static class IntegrationAccess
{
    public const byte PlatformAdministratorRole = 100;

    public static bool IsPlatformAdministrator(byte platformRole) =>
        platformRole >= PlatformAdministratorRole;

    /// <summary>Workspace admins may manage integrations for that workspace.</summary>
    public static async Task EnsureCanManageIntegrationsAsync(
        AppDbContext db,
        Guid workspaceId,
        int userId,
        CancellationToken cancellationToken)
    {
        var workspace = await db.Workspaces.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == workspaceId && item.DeletedAt == null, cancellationToken)
            ?? throw new UnauthorizedAccessException("دسترسی به این فضا مجاز نیست.");

        if (workspace.OwnerUserId == userId) return;

        var member = await db.WorkspaceMembers.AsNoTracking().FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.Status == WorkspaceMemberStatus.Active
                && item.DeletedAt == null,
            cancellationToken);

        if (member is null)
            throw new UnauthorizedAccessException("دسترسی به این فضا مجاز نیست.");

        if (workspace.Type == WorkspaceType.Personal)
        {
            // Personal: only owner (already checked) — members other than owner cannot manage.
            throw new UnauthorizedAccessException("فقط مالک فضا می‌تواند یکپارچه‌سازی را مدیریت کند.");
        }

        if (SchoolAccess.IsAdmin(member.Role)) return;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is not null && IsPlatformAdministrator(user.PlatformRole)) return;

        throw new UnauthorizedAccessException("فقط مدیران فضای کاری می‌توانند وب‌هوک/کلید API را مدیریت کنند.");
    }
}
