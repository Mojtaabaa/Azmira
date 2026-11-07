using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class TeamService(AppDbContext db)
{
    public async Task<object> ListAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var myTeamIds = await db.TeamMembers.AsNoTracking()
            .Where(item => item.UserId == userId && item.LeftAt == null)
            .Select(item => item.TeamId)
            .ToListAsync(cancellationToken);

        var teams = await db.Teams.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.DeletedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                description = item.Description,
                classId = item.ClassId,
                ownerUserId = item.OwnerUserId,
                memberCount = item.Members.Count(m => m.LeftAt == null),
                isMember = myTeamIds.Contains(item.Id),
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new { items = teams };
    }

    public async Task<object> CreateAsync(
        Guid workspaceId,
        int userId,
        string name,
        string? description,
        Guid? classId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام تیم لازم است.");
        if (classId is Guid cid)
        {
            var ok = await db.Classes.AnyAsync(item => item.Id == cid && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken);
            if (!ok) throw new InvalidOperationException("کلاس پیدا نشد.");
        }

        var now = DateTime.UtcNow;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClassId = classId,
            Name = name.Trim(),
            Description = description?.Trim() ?? "",
            OwnerUserId = userId,
            CreatedAt = now,
        };
        db.Teams.Add(team);
        db.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = userId,
            Role = TeamMemberRole.Captain,
            JoinedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { id = team.Id, name = team.Name };
    }

    public async Task<object> InviteAsync(Guid workspaceId, int actorUserId, Guid teamId, int invitedUserId, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(workspaceId, teamId, cancellationToken);
        await RequireCaptainOrOwnerAsync(team, actorUserId, cancellationToken);
        if (!await db.WorkspaceMembers.AnyAsync(
                item => item.WorkspaceId == workspaceId && item.UserId == invitedUserId && item.Status == WorkspaceMemberStatus.Active,
                cancellationToken))
        {
            throw new InvalidOperationException("کاربر عضو این فضا نیست.");
        }

        if (await db.TeamMembers.AnyAsync(item => item.TeamId == teamId && item.UserId == invitedUserId && item.LeftAt == null, cancellationToken))
        {
            throw new InvalidOperationException("کاربر از قبل عضو تیم است.");
        }

        var invite = new TeamInvite
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            InvitedUserId = invitedUserId,
            InvitedByUserId = actorUserId,
            Status = TeamInviteStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.TeamInvites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = invite.Id, status = invite.Status.ToString() };
    }

    public async Task<object> RespondInviteAsync(Guid workspaceId, int userId, Guid inviteId, bool accept, CancellationToken cancellationToken)
    {
        var invite = await db.TeamInvites.Include(item => item.Team)
            .FirstOrDefaultAsync(item => item.Id == inviteId && item.InvitedUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("دعوت پیدا نشد.");
        if (invite.Team.WorkspaceId != workspaceId || invite.Team.DeletedAt != null)
            throw new InvalidOperationException("تیم معتبر نیست.");
        if (invite.Status != TeamInviteStatus.Pending)
            throw new InvalidOperationException("این دعوت دیگر فعال نیست.");

        invite.Status = accept ? TeamInviteStatus.Accepted : TeamInviteStatus.Declined;
        invite.RespondedAt = DateTime.UtcNow;
        if (accept)
        {
            if (!await db.TeamMembers.AnyAsync(item => item.TeamId == invite.TeamId && item.UserId == userId && item.LeftAt == null, cancellationToken))
            {
                db.TeamMembers.Add(new TeamMember
                {
                    Id = Guid.NewGuid(),
                    TeamId = invite.TeamId,
                    UserId = userId,
                    Role = TeamMemberRole.Member,
                    JoinedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true, status = invite.Status.ToString() };
    }

    public async Task<object> JoinAsync(Guid workspaceId, int userId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(workspaceId, teamId, cancellationToken);
        if (await db.TeamMembers.AnyAsync(item => item.TeamId == team.Id && item.UserId == userId && item.LeftAt == null, cancellationToken))
        {
            return new { ok = true, already = true };
        }

        db.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = userId,
            Role = TeamMemberRole.Member,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true };
    }

    public async Task<object> CreateChallengeAsync(
        Guid workspaceId,
        int userId,
        Guid teamId,
        string title,
        string? description,
        int targetXp,
        DateTime startsAt,
        DateTime endsAt,
        CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(workspaceId, teamId, cancellationToken);
        await RequireCaptainOrOwnerAsync(team, userId, cancellationToken);
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان لازم است.");
        if (endsAt <= startsAt) throw new InvalidOperationException("بازه زمانی نامعتبر است.");

        var challenge = new TeamChallenge
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            TargetXp = Math.Max(1, targetXp),
            StartsAt = startsAt,
            EndsAt = endsAt,
            CreatedAt = DateTime.UtcNow,
        };
        db.TeamChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = challenge.Id };
    }

    public async Task<object> HistoryAsync(Guid workspaceId, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await RequireTeamAsync(workspaceId, teamId, cancellationToken);
        var members = await db.TeamMembers.AsNoTracking()
            .Where(item => item.TeamId == team.Id)
            .Select(item => new
            {
                userId = item.UserId,
                role = item.Role.ToString(),
                joinedAt = item.JoinedAt,
                leftAt = item.LeftAt,
            })
            .ToListAsync(cancellationToken);
        var challenges = await db.TeamChallenges.AsNoTracking()
            .Where(item => item.TeamId == team.Id)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                title = item.Title,
                targetXp = item.TargetXp,
                startsAt = item.StartsAt,
                endsAt = item.EndsAt,
            })
            .ToListAsync(cancellationToken);
        var achievements = await db.TeamAchievements.AsNoTracking()
            .Where(item => item.TeamId == team.Id)
            .OrderByDescending(item => item.AwardedAt)
            .Select(item => new { id = item.Id, code = item.Code, title = item.Title, awardedAt = item.AwardedAt })
            .ToListAsync(cancellationToken);

        var activeMembers = members.Where(item => item.leftAt == null).Select(item => item.userId).ToList();
        var xp = await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && activeMembers.Contains(item.UserId) && item.Kind == LedgerKind.Earn)
            .SumAsync(item => (int?)item.Delta, cancellationToken) ?? 0;

        return new
        {
            id = team.Id,
            name = team.Name,
            description = team.Description,
            teamXp = xp,
            members,
            challenges,
            achievements,
        };
    }

    private async Task<Team> RequireTeamAsync(Guid workspaceId, Guid teamId, CancellationToken cancellationToken) =>
        await db.Teams.FirstOrDefaultAsync(item => item.Id == teamId && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken)
        ?? throw new InvalidOperationException("تیم پیدا نشد.");

    private async Task RequireCaptainOrOwnerAsync(Team team, int userId, CancellationToken cancellationToken)
    {
        if (team.OwnerUserId == userId) return;
        var member = await db.TeamMembers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TeamId == team.Id && item.UserId == userId && item.LeftAt == null, cancellationToken);
        if (member?.Role == TeamMemberRole.Captain) return;
        throw new InvalidOperationException("فقط کاپیتان یا مالک تیم مجاز است.");
    }
}
