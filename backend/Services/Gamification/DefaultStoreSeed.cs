using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public static class DefaultStoreSeed
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.StoreItems.AsNoTracking()
            .Where(item => item.IsSystem && item.WorkspaceId == null)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        foreach (var item in Catalog(now))
        {
            if (set.Contains(item.Code)) continue;
            db.StoreItems.Add(item);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!await db.LeaderboardDefinitions.AnyAsync(cancellationToken))
        {
            foreach (LeaderboardMetric metric in Enum.GetValues<LeaderboardMetric>())
            {
                db.LeaderboardDefinitions.Add(new LeaderboardDefinition
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = null,
                    Metric = metric,
                    PeriodCode = metric is LeaderboardMetric.MonthlyXp ? "month" : "week",
                    Enabled = true,
                    Visibility = "members",
                    CreatedAt = now,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static IEnumerable<StoreItem> Catalog(DateTime now) =>
    [
        Free(now, "base-circle", "پایه دایره", StoreItemCategory.AvatarBase, "base-circle", "avatar-base avatar-base-circle"),
        Free(now, "base-hex", "پایه شش‌ضلعی", StoreItemCategory.AvatarBase, "base-hex", "avatar-base avatar-base-hex"),
        Paid(now, "hat-cap", "کلاه مطالعه", StoreItemCategory.Accessory, 40, "hat-cap", "avatar-acc avatar-acc-cap"),
        Paid(now, "glasses", "عینک تمرکز", StoreItemCategory.Accessory, 55, "glasses", "avatar-acc avatar-acc-glasses"),
        Paid(now, "hoodie", "هودی آبی", StoreItemCategory.Clothing, 80, "hoodie", "avatar-cloth avatar-cloth-hoodie"),
        Paid(now, "scarf", "شال سبز", StoreItemCategory.Clothing, 60, "scarf", "avatar-cloth avatar-cloth-scarf"),
        Paid(now, "bg-dawn", "پس‌زمینه سپیده‌دم", StoreItemCategory.Background, 70, "bg-dawn", "avatar-bg avatar-bg-dawn"),
        Paid(now, "bg-night", "پس‌زمینه شب", StoreItemCategory.Background, 70, "bg-night", "avatar-bg avatar-bg-night"),
        Paid(now, "frame-gold", "قاب طلایی", StoreItemCategory.Frame, 120, "frame-gold", "avatar-frame avatar-frame-gold"),
        Paid(now, "frame-mint", "قاب نعنایی", StoreItemCategory.Frame, 90, "frame-mint", "avatar-frame avatar-frame-mint"),
    ];

    private static StoreItem Free(DateTime now, string code, string title, StoreItemCategory category, string asset, string css) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = null,
            Code = code,
            Title = title,
            Description = "آیتم رایگان پایه",
            Category = category,
            PriceCoins = 0,
            AssetKey = asset,
            CssClass = css,
            Stackable = false,
            IsSystem = true,
            Active = true,
            CreatedAt = now,
        };

    private static StoreItem Paid(DateTime now, string code, string title, StoreItemCategory category, int price, string asset, string css) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkspaceId = null,
            Code = code,
            Title = title,
            Description = title,
            Category = category,
            PriceCoins = price,
            AssetKey = asset,
            CssClass = css,
            Stackable = false,
            IsSystem = true,
            Active = true,
            CreatedAt = now,
        };
}
