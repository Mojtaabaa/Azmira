using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class RewardService(AppDbContext db, XpEngine xp)
{
    public async Task<object> ListAsync(Guid workspaceId, int userId, bool adminView, CancellationToken cancellationToken)
    {
        var query = db.RewardGrants.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
        if (!adminView) query = query.Where(item => item.UserId == userId);

        var items = await query.OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new
            {
                id = item.Id,
                userId = item.UserId,
                kind = item.Kind.ToString(),
                status = item.Status.ToString(),
                title = item.Title,
                description = item.Description,
                coinAmount = item.CoinAmount,
                xpAmount = item.XpAmount,
                storeItemId = item.StoreItemId,
                competitionId = item.CompetitionId,
                createdAt = item.CreatedAt,
                resolvedAt = item.ResolvedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> CreateAsync(
        Guid workspaceId,
        int actorUserId,
        int userId,
        RewardGrantKind kind,
        string title,
        string? description,
        int? coinAmount,
        int? xpAmount,
        Guid? storeItemId,
        Guid? competitionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException("عنوان پاداش لازم است.");

        var needsApproval = kind is RewardGrantKind.School or RewardGrantKind.Teacher or RewardGrantKind.Physical or RewardGrantKind.Certificate;
        var grant = new RewardGrant
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Kind = kind,
            Status = needsApproval ? RewardGrantStatus.Pending : RewardGrantStatus.Approved,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            CoinAmount = coinAmount,
            XpAmount = xpAmount,
            StoreItemId = storeItemId,
            CompetitionId = competitionId,
            CreatedByUserId = actorUserId,
            ApprovedByUserId = needsApproval ? null : actorUserId,
            CreatedAt = DateTime.UtcNow,
            ResolvedAt = needsApproval ? null : DateTime.UtcNow,
        };
        db.RewardGrants.Add(grant);
        await db.SaveChangesAsync(cancellationToken);

        if (!needsApproval)
        {
            await FulfillAsync(grant, actorUserId, cancellationToken);
        }

        return new { id = grant.Id, status = grant.Status.ToString() };
    }

    public async Task<object> GrantDigitalAsync(
        Guid workspaceId,
        int userId,
        int actorUserId,
        string title,
        string description,
        int? coins,
        int? xpAmount,
        Guid? competitionId,
        CancellationToken cancellationToken) =>
        await CreateAsync(
            workspaceId,
            actorUserId,
            userId,
            RewardGrantKind.Digital,
            title,
            description,
            coins,
            xpAmount,
            null,
            competitionId,
            cancellationToken);

    public async Task<object> ApproveAsync(
        Guid workspaceId,
        int actorUserId,
        Guid grantId,
        bool approve,
        CancellationToken cancellationToken)
    {
        var grant = await db.RewardGrants.FirstOrDefaultAsync(
            item => item.Id == grantId && item.WorkspaceId == workspaceId,
            cancellationToken) ?? throw new InvalidOperationException("پاداش پیدا نشد.");

        if (grant.Status != RewardGrantStatus.Pending)
            throw new InvalidOperationException("این پاداش در انتظار تأیید نیست.");

        if (!approve)
        {
            grant.Status = RewardGrantStatus.Rejected;
            grant.ApprovedByUserId = actorUserId;
            grant.ResolvedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return new { id = grant.Id, status = grant.Status.ToString() };
        }

        grant.Status = RewardGrantStatus.Approved;
        grant.ApprovedByUserId = actorUserId;
        grant.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await FulfillAsync(grant, actorUserId, cancellationToken);
        return new { id = grant.Id, status = grant.Status.ToString() };
    }

    private async Task FulfillAsync(RewardGrant grant, int actorUserId, CancellationToken cancellationToken)
    {
        if (grant.XpAmount is int xpAmount and not 0)
        {
            await xp.AdjustAsync(grant.WorkspaceId, grant.UserId, xpAmount, $"reward:{grant.Id}", actorUserId, cancellationToken);
        }

        if (grant.CoinAmount is int coins and > 0)
        {
            var stats = await db.UserGameStats.FirstOrDefaultAsync(
                item => item.WorkspaceId == grant.WorkspaceId && item.UserId == grant.UserId,
                cancellationToken);
            if (stats is null)
            {
                stats = new UserGameStats
                {
                    WorkspaceId = grant.WorkspaceId,
                    UserId = grant.UserId,
                    Level = 1,
                    TimeZone = "Asia/Tehran",
                    UpdatedAt = DateTime.UtcNow,
                };
                db.UserGameStats.Add(stats);
            }

            stats.Coins += coins;
            stats.UpdatedAt = DateTime.UtcNow;
            db.CoinLedger.Add(new CoinLedger
            {
                Id = Guid.NewGuid(),
                WorkspaceId = grant.WorkspaceId,
                UserId = grant.UserId,
                Delta = coins,
                BalanceAfter = stats.Coins,
                Reason = $"reward:{grant.Id}",
                RuleCode = "reward",
                Kind = LedgerKind.Earn,
                CreatedAt = DateTime.UtcNow,
            });
        }

        if (grant.StoreItemId is Guid itemId)
        {
            if (!await db.UserInventory.AnyAsync(
                    item => item.WorkspaceId == grant.WorkspaceId && item.UserId == grant.UserId && item.ItemId == itemId,
                    cancellationToken))
            {
                db.UserInventory.Add(new UserInventoryItem
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = grant.WorkspaceId,
                    UserId = grant.UserId,
                    ItemId = itemId,
                    Quantity = 1,
                    AcquiredAt = DateTime.UtcNow,
                });
            }
        }

        grant.Status = RewardGrantStatus.Fulfilled;
        grant.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
