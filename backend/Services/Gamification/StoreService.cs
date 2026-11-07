using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class StoreService(AppDbContext db)
{
    public async Task<object> CatalogAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var items = await db.StoreItems.AsNoTracking()
            .Where(item => item.Active
                && (item.WorkspaceId == null || item.WorkspaceId == workspaceId)
                && (item.AvailableFrom == null || item.AvailableFrom <= now)
                && (item.AvailableTo == null || item.AvailableTo >= now))
            .OrderBy(item => item.Category)
            .ThenBy(item => item.PriceCoins)
            .Select(item => new
            {
                id = item.Id,
                code = item.Code,
                title = item.Title,
                description = item.Description,
                category = item.Category.ToString(),
                priceCoins = item.PriceCoins,
                assetKey = item.AssetKey,
                cssClass = item.CssClass,
                stackable = item.Stackable,
                isSystem = item.IsSystem,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task<object> InventoryAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        await EnsureFreeBasesAsync(workspaceId, userId, cancellationToken);

        var items = await db.UserInventory.AsNoTracking()
            .Include(item => item.Item)
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .Select(item => new
            {
                id = item.Id,
                itemId = item.ItemId,
                code = item.Item.Code,
                title = item.Item.Title,
                category = item.Item.Category.ToString(),
                assetKey = item.Item.AssetKey,
                cssClass = item.Item.CssClass,
                quantity = item.Quantity,
                equipped = item.Equipped,
                acquiredAt = item.AcquiredAt,
            })
            .ToListAsync(cancellationToken);

        var loadout = await db.UserAvatarLoadouts.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken);

        return new
        {
            items,
            loadout = loadout is null
                ? null
                : new
                {
                    baseItemId = loadout.BaseItemId,
                    accessoryItemId = loadout.AccessoryItemId,
                    clothingItemId = loadout.ClothingItemId,
                    backgroundItemId = loadout.BackgroundItemId,
                    frameItemId = loadout.FrameItemId,
                    updatedAt = loadout.UpdatedAt,
                },
        };
    }

    public async Task<object> PurchaseAsync(Guid workspaceId, int userId, Guid itemId, CancellationToken cancellationToken)
    {
        var useTx = db.Database.IsRelational();
        await using var tx = useTx ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var item = await db.StoreItems.FirstOrDefaultAsync(
            row => row.Id == itemId
                && row.Active
                && (row.WorkspaceId == null || row.WorkspaceId == workspaceId),
            cancellationToken) ?? throw new InvalidOperationException("آیتم فروشگاه پیدا نشد.");

        var now = DateTime.UtcNow;
        if (item.AvailableFrom is DateTime from && from > now) throw new InvalidOperationException("آیتم هنوز فعال نشده.");
        if (item.AvailableTo is DateTime to && to < now) throw new InvalidOperationException("آیتم منقضی شده.");

        var owned = await db.UserInventory.FirstOrDefaultAsync(
            row => row.WorkspaceId == workspaceId && row.UserId == userId && row.ItemId == itemId,
            cancellationToken);
        if (owned is not null && !item.Stackable)
            throw new InvalidOperationException("این آیتم غیرقابل تکرار است و قبلاً خریده شده.");

        var stats = await db.UserGameStats.FirstOrDefaultAsync(
            row => row.WorkspaceId == workspaceId && row.UserId == userId,
            cancellationToken) ?? throw new InvalidOperationException("سکه کافی نداری.");

        if (stats.Coins < item.PriceCoins)
            throw new InvalidOperationException("سکه کافی نداری.");

        stats.Coins -= item.PriceCoins;
        stats.UpdatedAt = now;
        var ledgerId = Guid.NewGuid();
        db.CoinLedger.Add(new CoinLedger
        {
            Id = ledgerId,
            WorkspaceId = workspaceId,
            UserId = userId,
            Delta = -item.PriceCoins,
            BalanceAfter = stats.Coins,
            Reason = $"store:{item.Code}",
            RuleCode = "store_purchase",
            Kind = LedgerKind.Adjust,
            CreatedAt = now,
        });

        if (owned is null)
        {
            db.UserInventory.Add(new UserInventoryItem
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                ItemId = itemId,
                Quantity = 1,
                AcquiredAt = now,
            });
        }
        else
        {
            owned.Quantity += 1;
        }

        db.StorePurchases.Add(new StorePurchase
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            ItemId = itemId,
            PriceCoins = item.PriceCoins,
            CoinLedgerId = ledgerId,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        if (tx is not null) await tx.CommitAsync(cancellationToken);
        return new { ok = true, coins = stats.Coins, itemId };
    }

    public async Task<object> ActivateAsync(
        Guid workspaceId,
        int userId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var owned = await db.UserInventory.Include(item => item.Item)
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.ItemId == itemId, cancellationToken)
            ?? throw new InvalidOperationException("این آیتم را نداری.");

        var loadout = await db.UserAvatarLoadouts.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (loadout is null)
        {
            loadout = new UserAvatarLoadout
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                UpdatedAt = DateTime.UtcNow,
            };
            db.UserAvatarLoadouts.Add(loadout);
        }

        switch (owned.Item.Category)
        {
            case StoreItemCategory.AvatarBase:
                loadout.BaseItemId = itemId;
                break;
            case StoreItemCategory.Accessory:
                loadout.AccessoryItemId = itemId;
                break;
            case StoreItemCategory.Clothing:
                loadout.ClothingItemId = itemId;
                break;
            case StoreItemCategory.Background:
                loadout.BackgroundItemId = itemId;
                break;
            case StoreItemCategory.Frame:
                loadout.FrameItemId = itemId;
                break;
            default:
                throw new InvalidOperationException("این آیتم قابل فعال‌سازی روی آواتار نیست.");
        }

        var inventory = await db.UserInventory
            .Include(item => item.Item)
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        foreach (var row in inventory)
        {
            row.Equipped = row.ItemId == itemId
                || (row.Item.Category != owned.Item.Category && row.Equipped);
            if (row.Item.Category == owned.Item.Category)
            {
                row.Equipped = row.ItemId == itemId;
            }
        }

        loadout.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            ok = true,
            loadout = new
            {
                baseItemId = loadout.BaseItemId,
                accessoryItemId = loadout.AccessoryItemId,
                clothingItemId = loadout.ClothingItemId,
                backgroundItemId = loadout.BackgroundItemId,
                frameItemId = loadout.FrameItemId,
            },
        };
    }

    public async Task<object> AdminUpsertAsync(
        Guid? workspaceId,
        string code,
        string title,
        string? description,
        StoreItemCategory category,
        int priceCoins,
        string assetKey,
        string cssClass,
        bool stackable,
        bool isSystem,
        bool active,
        DateTime? availableFrom,
        DateTime? availableTo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("کد و عنوان لازم است.");

        var existing = await db.StoreItems.FirstOrDefaultAsync(
            item => item.Code == code && item.WorkspaceId == workspaceId,
            cancellationToken);
        if (existing is null)
        {
            existing = new StoreItem
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                Code = code.Trim(),
                CreatedAt = DateTime.UtcNow,
            };
            db.StoreItems.Add(existing);
        }

        existing.Title = title.Trim();
        existing.Description = description?.Trim() ?? "";
        existing.Category = category;
        existing.PriceCoins = Math.Max(0, priceCoins);
        existing.AssetKey = assetKey?.Trim() ?? code.Trim();
        existing.CssClass = cssClass?.Trim() ?? $"avatar-{code.Trim()}";
        existing.Stackable = stackable;
        existing.IsSystem = isSystem;
        existing.Active = active;
        existing.AvailableFrom = availableFrom;
        existing.AvailableTo = availableTo;
        await db.SaveChangesAsync(cancellationToken);
        return new { id = existing.Id, code = existing.Code };
    }

    private async Task EnsureFreeBasesAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var freeBases = await db.StoreItems.AsNoTracking()
            .Where(item => item.IsSystem
                && item.Active
                && item.PriceCoins == 0
                && item.Category == StoreItemCategory.AvatarBase
                && item.WorkspaceId == null)
            .ToListAsync(cancellationToken);
        if (freeBases.Count == 0) return;

        var owned = await db.UserInventory.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.UserId == userId)
            .Select(item => item.ItemId)
            .ToListAsync(cancellationToken);
        var ownedSet = owned.ToHashSet();
        var added = false;
        foreach (var item in freeBases)
        {
            if (ownedSet.Contains(item.Id)) continue;
            db.UserInventory.Add(new UserInventoryItem
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                ItemId = item.Id,
                Quantity = 1,
                Equipped = !added,
                AcquiredAt = DateTime.UtcNow,
            });
            added = true;
        }

        if (!added) return;

        var loadout = await db.UserAvatarLoadouts.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (loadout is null)
        {
            db.UserAvatarLoadouts.Add(new UserAvatarLoadout
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                BaseItemId = freeBases[0].Id,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else if (loadout.BaseItemId is null)
        {
            loadout.BaseItemId = freeBases[0].Id;
            loadout.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
