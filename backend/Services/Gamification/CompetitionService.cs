using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class CompetitionService(AppDbContext db, RewardService rewards)
{
    public async Task<object> ListAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var items = await db.Competitions.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                description = item.Description,
                type = item.Type.ToString(),
                status = item.Status.ToString(),
                classId = item.ClassId,
                startsAt = item.StartsAt,
                endsAt = item.EndsAt,
                createdByUserId = item.CreatedByUserId,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateAsync(
        Guid workspaceId,
        int userId,
        string name,
        string? description,
        CompetitionType type,
        Guid? classId,
        DateTime startsAt,
        DateTime endsAt,
        string? scoringRulesJson,
        string? rewardsJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام مسابقه لازم است.");
        if (endsAt <= startsAt) throw new InvalidOperationException("بازه زمانی نامعتبر است.");
        if (classId is Guid cid)
        {
            var ok = await db.Classes.AnyAsync(item => item.Id == cid && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken);
            if (!ok) throw new InvalidOperationException("کلاس پیدا نشد.");
        }

        var now = DateTime.UtcNow;
        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ClassId = classId,
            Name = name.Trim(),
            Description = description?.Trim() ?? "",
            Type = type,
            Status = CompetitionStatus.Draft,
            StartsAt = startsAt,
            EndsAt = endsAt,
            ScoringRulesJson = string.IsNullOrWhiteSpace(scoringRulesJson) ? """{"metric":"weeklyXp"}""" : scoringRulesJson,
            RewardsJson = string.IsNullOrWhiteSpace(rewardsJson) ? "{}" : rewardsJson,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Competitions.Add(competition);
        await db.SaveChangesAsync(cancellationToken);
        return new { id = competition.Id, status = competition.Status.ToString() };
    }

    public async Task<object> UpdateStatusAsync(Guid workspaceId, Guid id, CompetitionStatus status, CancellationToken cancellationToken)
    {
        var competition = await RequireAsync(workspaceId, id, cancellationToken);
        competition.Status = status;
        competition.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (status == CompetitionStatus.Ended)
        {
            await FinalizeRewardsAsync(competition, cancellationToken);
        }

        return new { id = competition.Id, status = competition.Status.ToString() };
    }

    public async Task<object> JoinAsync(Guid workspaceId, int userId, Guid competitionId, Guid? teamId, CancellationToken cancellationToken)
    {
        var competition = await RequireAsync(workspaceId, competitionId, cancellationToken);
        if (competition.Status is not CompetitionStatus.Active and not CompetitionStatus.Draft)
            throw new InvalidOperationException("امکان پیوستن وجود ندارد.");

        if (competition.Type == CompetitionType.Team)
        {
            if (teamId is not Guid tid) throw new InvalidOperationException("teamId لازم است.");
            var teamOk = await db.Teams.AnyAsync(item => item.Id == tid && item.WorkspaceId == workspaceId && item.DeletedAt == null, cancellationToken);
            if (!teamOk) throw new InvalidOperationException("تیم پیدا نشد.");
            if (!await db.TeamMembers.AnyAsync(item => item.TeamId == tid && item.UserId == userId && item.LeftAt == null, cancellationToken))
                throw new InvalidOperationException("عضو این تیم نیستی.");
            if (!await db.CompetitionTeams.AnyAsync(item => item.CompetitionId == competitionId && item.TeamId == tid, cancellationToken))
            {
                db.CompetitionTeams.Add(new CompetitionTeam
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    TeamId = tid,
                    JoinedAt = DateTime.UtcNow,
                });
            }
        }
        else
        {
            if (!await db.CompetitionParticipants.AnyAsync(item => item.CompetitionId == competitionId && item.UserId == userId, cancellationToken))
            {
                db.CompetitionParticipants.Add(new CompetitionParticipant
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competitionId,
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true };
    }

    public async Task<object> ResultsAsync(Guid workspaceId, Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await RequireAsync(workspaceId, competitionId, cancellationToken);
        await RefreshScoresAsync(competition, cancellationToken);

        if (competition.Type == CompetitionType.Team)
        {
            var teams = await db.CompetitionTeams.AsNoTracking()
                .Where(item => item.CompetitionId == competitionId)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.JoinedAt)
                .Select(item => new { teamId = item.TeamId, score = item.Score, joinedAt = item.JoinedAt })
                .ToListAsync(cancellationToken);
            return new { type = "Team", status = competition.Status.ToString(), entries = teams };
        }

        var people = await db.CompetitionParticipants.AsNoTracking()
            .Where(item => item.CompetitionId == competitionId)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.JoinedAt)
            .Select(item => new { userId = item.UserId, score = item.Score, joinedAt = item.JoinedAt })
            .ToListAsync(cancellationToken);
        return new { type = "Individual", status = competition.Status.ToString(), entries = people };
    }

    private async Task RefreshScoresAsync(Competition competition, CancellationToken cancellationToken)
    {
        var metric = "weeklyXp";
        try
        {
            using var doc = JsonDocument.Parse(competition.ScoringRulesJson);
            if (doc.RootElement.TryGetProperty("metric", out var m) && m.ValueKind == JsonValueKind.String)
            {
                metric = m.GetString() ?? metric;
            }
        }
        catch
        {
            // keep default
        }

        if (competition.Type == CompetitionType.Team)
        {
            var rows = await db.CompetitionTeams.Where(item => item.CompetitionId == competition.Id).ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                var memberIds = await db.TeamMembers.AsNoTracking()
                    .Where(item => item.TeamId == row.TeamId && item.LeftAt == null)
                    .Select(item => item.UserId)
                    .ToListAsync(cancellationToken);
                row.Score = await SumXpAsync(competition.WorkspaceId, memberIds, competition.StartsAt, competition.EndsAt, cancellationToken);
            }
        }
        else
        {
            var rows = await db.CompetitionParticipants.Where(item => item.CompetitionId == competition.Id).ToListAsync(cancellationToken);
            foreach (var row in rows)
            {
                row.Score = await SumXpAsync(competition.WorkspaceId, [row.UserId], competition.StartsAt, competition.EndsAt, cancellationToken);
            }
        }

        _ = metric;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FinalizeRewardsAsync(Competition competition, CancellationToken cancellationToken)
    {
        await RefreshScoresAsync(competition, cancellationToken);
        int? coins = null;
        int? xpAmount = null;
        try
        {
            using var doc = JsonDocument.Parse(competition.RewardsJson);
            if (doc.RootElement.TryGetProperty("coins", out var c) && c.TryGetInt32(out var coinVal)) coins = coinVal;
            if (doc.RootElement.TryGetProperty("xp", out var x) && x.TryGetInt32(out var xpVal)) xpAmount = xpVal;
        }
        catch
        {
            // ignore
        }

        if (competition.Type == CompetitionType.Individual)
        {
            var winner = await db.CompetitionParticipants
                .Where(item => item.CompetitionId == competition.Id)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.JoinedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (winner is null) return;
            await rewards.GrantDigitalAsync(
                competition.WorkspaceId,
                winner.UserId,
                competition.CreatedByUserId,
                $"برنده {competition.Name}",
                "پاداش پایان مسابقه",
                coins,
                xpAmount,
                competition.Id,
                cancellationToken);
        }
    }

    private async Task<int> SumXpAsync(Guid workspaceId, List<int> userIds, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0) return 0;
        return await db.XpLedger.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId
                && userIds.Contains(item.UserId)
                && item.Kind == LedgerKind.Earn
                && item.CreatedAt >= from
                && item.CreatedAt < to)
            .SumAsync(item => (int?)item.Delta, cancellationToken) ?? 0;
    }

    private async Task<Competition> RequireAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        await db.Competitions.FirstOrDefaultAsync(item => item.Id == id && item.WorkspaceId == workspaceId, cancellationToken)
        ?? throw new InvalidOperationException("مسابقه پیدا نشد.");
}
