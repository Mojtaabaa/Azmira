using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public sealed class WorkspaceService(AppDbContext db)
{
    public const string HeaderName = "X-Rotbeh-Workspace";
    public const string ItemKey = "workspace";

    public async Task<WorkspaceContext> EnsurePersonalAsync(int userId, CancellationToken cancellationToken)
    {
        var existing = await FindPersonalAsync(userId, cancellationToken);
        if (existing is not null)
        {
            await BackfillUserDataAsync(userId, existing.Id, cancellationToken);
            return await ToContextAsync(existing.Id, userId, cancellationToken)
                ?? throw new InvalidOperationException("عضویت فضای شخصی پیدا نشد.");
        }

        var now = DateTime.UtcNow;
        var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        var settingsJson = await db.Settings.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.Data)
            .FirstOrDefaultAsync(cancellationToken) ?? "{}";

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Type = WorkspaceType.Personal,
            Name = "فضای شخصی",
            OwnerUserId = userId,
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = userId,
            Role = WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = workspace.Id,
            Data = settingsJson,
            LocaleDefault = string.IsNullOrWhiteSpace(user.Locale) ? "fa-IR" : user.Locale,
            TimeZone = "Asia/Tehran",
            FeatureFlags = "{}",
            UpdatedAt = now,
        });
        db.WorkspaceSubscriptions.Add(new WorkspaceSubscription
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            PlanCode = "free",
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });

        if (user.LastWorkspaceId is null) user.LastWorkspaceId = workspace.Id;
        await db.SaveChangesAsync(cancellationToken);
        await BackfillUserDataAsync(userId, workspace.Id, cancellationToken);

        return new WorkspaceContext(workspace.Id, WorkspaceType.Personal, WorkspaceMemberRole.Student, userId, workspace.Name);
    }

    public async Task BootstrapAllAsync(CancellationToken cancellationToken)
    {
        var userIds = await db.Users.AsNoTracking()
            .Where(user => user.DeletedAt == null)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            await EnsurePersonalAsync(userId, cancellationToken);
        }
    }

    public async Task<WorkspaceContext> ResolveAsync(int userId, string? headerValue, CancellationToken cancellationToken)
    {
        var personal = await EnsurePersonalAsync(userId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            if (!Guid.TryParse(headerValue.Trim(), out var requestedId))
            {
                throw new WorkspaceForbiddenException();
            }

            var context = await ToContextAsync(requestedId, userId, cancellationToken);
            if (context is null) throw new WorkspaceForbiddenException();
            return context;
        }

        var user = await db.Users.AsNoTracking().FirstAsync(item => item.Id == userId, cancellationToken);
        if (user.LastWorkspaceId is Guid lastId)
        {
            var preferred = await ToContextAsync(lastId, userId, cancellationToken);
            if (preferred is not null) return preferred;
        }

        return personal;
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> ListForUserAsync(int userId, CancellationToken cancellationToken)
    {
        await EnsurePersonalAsync(userId, cancellationToken);
        return await db.WorkspaceMembers.AsNoTracking()
            .Where(member => member.UserId == userId
                && member.DeletedAt == null
                && member.Status == WorkspaceMemberStatus.Active
                && member.Workspace.DeletedAt == null
                && member.Workspace.Status == WorkspaceStatus.Active)
            .OrderBy(member => member.Workspace.Type)
            .ThenBy(member => member.Workspace.Name)
            .Select(member => new WorkspaceListItem(
                member.WorkspaceId,
                member.Workspace.Type,
                member.Workspace.Name,
                member.Role))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkspaceContext> SwitchAsync(int userId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var context = await ToContextAsync(workspaceId, userId, cancellationToken);
        if (context is null) throw new WorkspaceForbiddenException();

        var user = await db.Users.FirstAsync(item => item.Id == userId, cancellationToken);
        user.LastWorkspaceId = workspaceId;
        await db.SaveChangesAsync(cancellationToken);
        return context;
    }

    public async Task BackfillUserDataAsync(int userId, Guid workspaceId, CancellationToken cancellationToken)
    {
        if (db.Database.IsRelational())
        {
            await db.StudyRecords.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            await db.Notes.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            await db.Folders.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            await db.Trash.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            await db.NoteImages.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            await db.NoteSettings.Where(item => item.UserId == userId && item.WorkspaceId == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.WorkspaceId, workspaceId), cancellationToken);
            return;
        }

        // In-memory / test providers lack ExecuteUpdate; update tracked entities instead.
        foreach (var row in await db.StudyRecords.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        foreach (var row in await db.Notes.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        foreach (var row in await db.Folders.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        foreach (var row in await db.Trash.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        foreach (var row in await db.NoteImages.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        foreach (var row in await db.NoteSettings.Where(item => item.UserId == userId && item.WorkspaceId == null).ToListAsync(cancellationToken))
            row.WorkspaceId = workspaceId;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Workspace?> FindPersonalAsync(int userId, CancellationToken cancellationToken) =>
        await db.Workspaces.FirstOrDefaultAsync(
            workspace => workspace.OwnerUserId == userId
                && workspace.Type == WorkspaceType.Personal
                && workspace.DeletedAt == null,
            cancellationToken);

    private async Task<WorkspaceContext?> ToContextAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var row = await db.WorkspaceMembers.AsNoTracking()
            .Where(member => member.WorkspaceId == workspaceId
                && member.UserId == userId
                && member.DeletedAt == null
                && member.Status == WorkspaceMemberStatus.Active
                && member.Workspace.DeletedAt == null
                && member.Workspace.Status == WorkspaceStatus.Active)
            .Select(member => new
            {
                member.WorkspaceId,
                member.Workspace.Type,
                member.Role,
                member.Workspace.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new WorkspaceContext(row.WorkspaceId, row.Type, row.Role, userId, row.Name);
    }
}

public sealed record WorkspaceListItem(Guid Id, WorkspaceType Type, string Name, WorkspaceMemberRole Role);

public sealed class WorkspaceForbiddenException : Exception
{
    public WorkspaceForbiddenException() : base("دسترسی به این فضا مجاز نیست.")
    {
    }
}
