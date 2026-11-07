using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class ClassService(AppDbContext db)
{
    public async Task<object> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var items = await db.Classes.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null)
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                createdByUserId = item.CreatedByUserId,
                memberCount = item.Members.Count,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateAsync(Guid workspaceId, int userId, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام کلاس لازم است.");
        var now = DateTime.UtcNow;
        var group = new ClassGroup
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        db.Classes.Add(group);
        db.ClassMembers.Add(new ClassMember
        {
            Id = Guid.NewGuid(),
            ClassId = group.Id,
            UserId = userId,
            Role = ClassMemberRole.Teacher,
            JoinedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { id = group.Id, name = group.Name };
    }

    public async Task<object> AddMemberAsync(
        Guid workspaceId,
        Guid classId,
        int userId,
        ClassMemberRole role,
        CancellationToken cancellationToken)
    {
        var group = await db.Classes.FirstOrDefaultAsync(
            item => item.Id == classId && item.WorkspaceId == workspaceId && item.DeletedAt == null,
            cancellationToken) ?? throw new InvalidOperationException("کلاس پیدا نشد.");

        if (!await db.WorkspaceMembers.AnyAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == userId && item.Status == WorkspaceMemberStatus.Active,
                cancellationToken))
        {
            throw new InvalidOperationException("کاربر عضو این فضا نیست.");
        }

        if (await db.ClassMembers.AnyAsync(item => item.ClassId == group.Id && item.UserId == userId, cancellationToken))
        {
            return new { ok = true, already = true };
        }

        db.ClassMembers.Add(new ClassMember
        {
            Id = Guid.NewGuid(),
            ClassId = group.Id,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true };
    }
}
