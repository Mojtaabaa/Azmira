using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public sealed class PlatformAdminService(AppDbContext db, SchoolService schools)
{
    public async Task<object> DashboardAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().CountAsync(item => item.DeletedAt == null, cancellationToken);
        var admins = await db.Users.AsNoTracking()
            .CountAsync(item => item.DeletedAt == null && item.PlatformRole >= PlatformRoles.Administrator, cancellationToken);
        var schools = await db.Schools.AsNoTracking().CountAsync(item => item.DeletedAt == null, cancellationToken);
        var pending = await db.SchoolRegistrationRequests.AsNoTracking()
            .CountAsync(item => item.Status == SchoolRegistrationRequestStatus.Pending, cancellationToken);
        return new
        {
            users,
            platformAdmins = admins,
            schools,
            pendingSchoolRequests = pending,
        };
    }

    public async Task<object> ListUsersAsync(string? query, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);
        var q = (query ?? "").Trim().ToLowerInvariant();
        var users = db.Users.AsNoTracking().Where(item => item.DeletedAt == null);
        if (!string.IsNullOrEmpty(q))
        {
            users = users.Where(item =>
                item.NormalizedUsername.Contains(q)
                || (item.DisplayName != null && item.DisplayName.ToLower().Contains(q))
                || item.Id.ToString() == q);
        }

        var items = await users
            .OrderByDescending(item => item.PlatformRole)
            .ThenBy(item => item.Username)
            .Take(take)
            .Select(item => new
            {
                id = item.Id,
                username = item.Username,
                displayName = item.DisplayName,
                platformRole = item.PlatformRole,
                isPlatformAdmin = item.PlatformRole >= PlatformRoles.Administrator,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> SetPlatformRoleAsync(int actorUserId, int targetUserId, byte platformRole, CancellationToken cancellationToken)
    {
        if (platformRole != 0 && platformRole < PlatformRoles.Administrator)
            throw new InvalidOperationException("نقش سراسری فقط ۰ (عادی) یا ۱۰۰+ (ادمین سامانه) مجاز است.");

        var target = await db.Users.FirstOrDefaultAsync(item => item.Id == targetUserId && item.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("کاربر پیدا نشد.");

        var wasAdmin = PlatformRoles.IsAdministrator(target.PlatformRole);
        var willBeAdmin = PlatformRoles.IsAdministrator(platformRole);
        if (wasAdmin && !willBeAdmin)
        {
            var adminCount = await db.Users.CountAsync(
                item => item.DeletedAt == null && item.PlatformRole >= PlatformRoles.Administrator,
                cancellationToken);
            if (adminCount <= 1)
                throw new InvalidOperationException("نمی‌توان آخرین ادمین سامانه را حذف کرد.");
            if (targetUserId == actorUserId && adminCount <= 1)
                throw new InvalidOperationException("نمی‌توانی خودت را به‌عنوان آخرین ادمین حذف کنی.");
        }

        target.PlatformRole = platformRole;
        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            ok = true,
            id = target.Id,
            username = target.Username,
            platformRole = target.PlatformRole,
            isPlatformAdmin = PlatformRoles.IsAdministrator(target.PlatformRole),
        };
    }

    public async Task<object> CreateSchoolForOwnerAsync(
        int ownerUserId,
        string name,
        string? city,
        string? description,
        CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(item => item.Id == ownerUserId && item.DeletedAt == null, cancellationToken))
            throw new InvalidOperationException("مالک مدرسه پیدا نشد.");
        var created = await schools.RegisterAsync(ownerUserId, name, city, description, cancellationToken);
        return new
        {
            schoolId = created.SchoolId,
            workspaceId = created.WorkspaceId,
            name = created.Name,
            slug = created.Slug,
            academicYearId = created.AcademicYearId,
        };
    }
}
