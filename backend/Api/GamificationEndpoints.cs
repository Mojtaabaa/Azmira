using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class GamificationEndpoints
{
    public static RouteGroupBuilder MapGamification(this RouteGroupBuilder group)
    {
        group.MapGet("/gamification/me", async (HttpContext http, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await query.GetProfileAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapGet("/gamification/ledger", async (HttpContext http, GamificationQueryService query, int? take, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await query.GetLedgerAsync(ws.WorkspaceId, ws.UserId, take ?? 50, cancellationToken));
        });

        group.MapGet("/gamification/notifications", async (HttpContext http, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await query.GetNotificationsAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPost("/gamification/notifications/{id:guid}/read", async (HttpContext http, Guid id, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            await query.MarkNotificationReadAsync(ws.WorkspaceId, ws.UserId, id, cancellationToken);
            return Results.Json(new { ok = true });
        });

        group.MapGet("/quests", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var now = DateTime.UtcNow;
            var catalog = await db.Quests.AsNoTracking()
                .Where(item => item.Status == QuestStatus.Active
                    && (item.WorkspaceId == null || item.WorkspaceId == ws.WorkspaceId)
                    && (item.AssignedUserId == null || item.AssignedUserId == ws.UserId)
                    && (item.StartsAt == null || item.StartsAt <= now)
                    && (item.EndsAt == null || item.EndsAt >= now))
                .ToListAsync(cancellationToken);
            var progress = await db.UserQuestProgress.AsNoTracking()
                .Where(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == ws.UserId)
                .ToDictionaryAsync(item => item.QuestId, cancellationToken);

            return Results.Json(new
            {
                items = catalog.Select(quest =>
                {
                    progress.TryGetValue(quest.Id, out var row);
                    return new
                    {
                        id = quest.Id,
                        title = quest.Title,
                        description = quest.Description,
                        category = quest.Category,
                        kind = quest.Kind.ToString(),
                        targetValue = quest.TargetValue,
                        progress = row?.Progress ?? 0,
                        rewardXp = quest.RewardXp,
                        rewardCoins = quest.RewardCoins,
                        status = (row?.Status ?? QuestStatus.Active).ToString(),
                        difficulty = quest.Difficulty,
                        endsAt = quest.EndsAt,
                    };
                }),
            });
        });

        group.MapPost("/quests/{id:guid}/claim", async (HttpContext http, Guid id, QuestEngine quests, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                await quests.ClaimAsync(ws.WorkspaceId, ws.UserId, id, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/achievements", async (HttpContext http, AppDbContext db, GamificationProfileService profiles, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var profile = await profiles.ResolveAsync(ws.WorkspaceId, cancellationToken);
            var badges = await db.Badges.AsNoTracking()
                .Where(item => item.ProfileId == profile.Id || item.WorkspaceId == ws.WorkspaceId)
                .ToListAsync(cancellationToken);
            var earned = await db.UserBadges.AsNoTracking()
                .Where(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == ws.UserId)
                .Select(item => item.BadgeId)
                .ToListAsync(cancellationToken);
            var earnedSet = earned.ToHashSet();
            return Results.Json(new
            {
                items = badges.Select(badge => new
                {
                    id = badge.Id,
                    code = badge.Code,
                    title = badge.IsHidden && !earnedSet.Contains(badge.Id) ? "نشان مخفی" : badge.Title,
                    description = badge.IsHidden && !earnedSet.Contains(badge.Id) ? "هنوز باز نشده." : badge.Description,
                    tier = badge.Tier.ToString().ToLowerInvariant(),
                    icon = badge.Icon,
                    hidden = badge.IsHidden,
                    earned = earnedSet.Contains(badge.Id),
                }),
            });
        });

        group.MapPost("/gamification/streak/rest-day", async (HttpContext http, StreakEngine streaks, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var dateText = body is not null && body.RootElement.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? ""
                : "";
            if (!DateOnly.TryParse(dateText, out var date)) return Results.Json(new { error = "تاریخ درست نیست." }, statusCode: 400);
            try
            {
                await streaks.SetRestDayAsync(ws.WorkspaceId, ws.UserId, date, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/gamification/streak/freeze", async (HttpContext http, StreakEngine streaks, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var dateText = body is not null && body.RootElement.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String
                ? d.GetString() ?? ""
                : DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
            if (!DateOnly.TryParse(dateText, out var date)) return Results.Json(new { error = "تاریخ درست نیست." }, statusCode: 400);
            try
            {
                await streaks.UseFreezeAsync(ws.WorkspaceId, ws.UserId, date, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/leaderboards/weekly", async (HttpContext http, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await query.WeeklyLeaderboardAsync(ws.WorkspaceId, cancellationToken));
        });

        MapAdmin(group);
        return group;
    }

    private static void MapAdmin(RouteGroupBuilder group)
    {
        group.MapGet("/admin/gamification/profile", async (HttpContext http, AppDbContext db, GamificationProfileService profiles, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var profile = await profiles.ResolveAsync(ws.WorkspaceId, cancellationToken);
            return Results.Json(new
            {
                id = profile.Id,
                code = profile.Code,
                name = profile.Name,
                isSystem = profile.IsSystem,
                dailyXpCap = profile.DailyXpCap,
                rules = JsonSerializer.Deserialize<object>(profile.RulesJson),
                levels = JsonSerializer.Deserialize<object>(profile.LevelsJson),
                streak = JsonSerializer.Deserialize<object>(profile.StreakJson),
            });
        });

        group.MapPut("/admin/gamification/profile", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || body.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Results.Json(new { error = "بدنه نامعتبر است." }, statusCode: 400);
            }

            var root = body.RootElement;
            var settings = await db.WorkspaceSettings.FirstAsync(item => item.WorkspaceId == ws.WorkspaceId, cancellationToken);
            GamificationProfile profile;
            if (settings.GamificationProfileId is Guid existingId)
            {
                profile = await db.GamificationProfiles.FirstAsync(item => item.Id == existingId, cancellationToken);
                if (profile.IsSystem)
                {
                    profile = CloneProfile(profile, ws.WorkspaceName);
                    db.GamificationProfiles.Add(profile);
                    settings.GamificationProfileId = profile.Id;
                }
            }
            else
            {
                var system = await db.GamificationProfiles.FirstAsync(item => item.Code == DefaultGamificationSeed.DefaultProfileCode, cancellationToken);
                profile = CloneProfile(system, ws.WorkspaceName);
                db.GamificationProfiles.Add(profile);
                settings.GamificationProfileId = profile.Id;
            }

            if (root.TryGetProperty("dailyXpCap", out var cap) && cap.TryGetInt32(out var capValue) && capValue > 0)
            {
                profile.DailyXpCap = capValue;
            }

            if (root.TryGetProperty("rules", out var rules)) profile.RulesJson = rules.GetRawText();
            if (root.TryGetProperty("levels", out var levels)) profile.LevelsJson = levels.GetRawText();
            if (root.TryGetProperty("streak", out var streak)) profile.StreakJson = streak.GetRawText();
            if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            {
                profile.Name = name.GetString() ?? profile.Name;
            }

            profile.UpdatedAt = DateTime.UtcNow;
            settings.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Json(new { ok = true, profileId = profile.Id });
        });

        group.MapPost("/admin/gamification/xp/adjust", async (HttpContext http, AppDbContext db, XpEngine xp, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null) return Results.Json(new { error = "بدنه نامعتبر است." }, statusCode: 400);
            if (!body.RootElement.TryGetProperty("userId", out var userEl) || !userEl.TryGetInt32(out var userId))
            {
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            }

            if (!body.RootElement.TryGetProperty("delta", out var deltaEl) || !deltaEl.TryGetInt32(out var delta) || delta == 0)
            {
                return Results.Json(new { error = "delta نامعتبر است." }, statusCode: 400);
            }

            var reason = body.RootElement.TryGetProperty("reason", out var reasonEl) && reasonEl.ValueKind == JsonValueKind.String
                ? reasonEl.GetString() ?? "admin"
                : "admin";
            var (balance, ledgerId) = await xp.AdjustAsync(ws.WorkspaceId, userId, delta, reason, ws.UserId, cancellationToken);
            return Results.Json(new { ok = true, balanceAfter = balance, ledgerId });
        });

        group.MapGet("/admin/gamification/badges", async (HttpContext http, AppDbContext db, GamificationProfileService profiles, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var profile = await profiles.ResolveAsync(ws.WorkspaceId, cancellationToken);
            var items = await db.Badges.AsNoTracking()
                .Where(item => item.ProfileId == profile.Id || item.WorkspaceId == ws.WorkspaceId)
                .Select(item => new
                {
                    id = item.Id,
                    code = item.Code,
                    title = item.Title,
                    description = item.Description,
                    tier = item.Tier.ToString().ToLowerInvariant(),
                    hidden = item.IsHidden,
                    allowRepeat = item.AllowRepeat,
                    conditions = item.ConditionsJson,
                })
                .ToListAsync(cancellationToken);
            return Results.Json(new { items });
        });

        group.MapGet("/admin/gamification/quests", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var items = await db.Quests.AsNoTracking()
                .Where(item => item.WorkspaceId == null || item.WorkspaceId == ws.WorkspaceId)
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    description = item.Description,
                    kind = item.Kind.ToString(),
                    targetValue = item.TargetValue,
                    rewardXp = item.RewardXp,
                    rewardCoins = item.RewardCoins,
                    status = item.Status.ToString(),
                    completionCondition = item.CompletionCondition,
                    endsAt = item.EndsAt,
                })
                .ToListAsync(cancellationToken);
            return Results.Json(new { items });
        });
    }

    private static GamificationProfile CloneProfile(GamificationProfile source, string workspaceName) => new()
    {
        Id = Guid.NewGuid(),
        Code = $"ws-{Guid.NewGuid():N}"[..20],
        Name = $"پروفایل {workspaceName}",
        IsSystem = false,
        RulesJson = source.RulesJson,
        LevelsJson = source.LevelsJson,
        StreakJson = source.StreakJson,
        DailyXpCap = source.DailyXpCap,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static async Task<IResult?> RequireAdmin(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return null;
        if (ws.Role is WorkspaceMemberRole.SchoolOwner or WorkspaceMemberRole.SchoolAdministrator) return null;
        return Results.Json(new { error = "دسترسی مدیریتی نداری." }, statusCode: 403);
    }
}
