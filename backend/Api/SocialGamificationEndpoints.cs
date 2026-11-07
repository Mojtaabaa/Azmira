using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class SocialGamificationEndpoints
{
    public static RouteGroupBuilder MapSocialGamification(this RouteGroupBuilder group)
    {
        group.MapGet("/leaderboards", async (
            HttpContext http,
            LeaderboardService boards,
            string? metric,
            string? period,
            string? subject,
            Guid? classId,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            if (!TryParseMetric(metric, out var parsed))
            {
                return Results.Json(new { error = "metric نامعتبر است." }, statusCode: 400);
            }

            return Results.Json(await boards.GetBoardAsync(
                ws.WorkspaceId,
                parsed,
                period ?? "week",
                subject,
                classId,
                page ?? 1,
                pageSize ?? 20,
                cancellationToken));
        });

        group.MapGet("/leaderboards/snapshots", async (
            HttpContext http,
            LeaderboardService boards,
            string? metric,
            string? period,
            CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            LeaderboardMetric? parsed = null;
            if (!string.IsNullOrWhiteSpace(metric))
            {
                if (!TryParseMetric(metric, out var m)) return Results.Json(new { error = "metric نامعتبر است." }, statusCode: 400);
                parsed = m;
            }

            return Results.Json(await boards.ListSnapshotsAsync(ws.WorkspaceId, parsed, period, cancellationToken));
        });

        group.MapPost("/leaderboards/snapshots", async (
            HttpContext http,
            AppDbContext db,
            LeaderboardService boards,
            CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolStaff(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var metricText = ReadString(body, "metric") ?? "WeeklyXp";
            if (!TryParseMetric(metricText, out var metric)) return Results.Json(new { error = "metric نامعتبر است." }, statusCode: 400);
            var period = ReadString(body, "period") ?? "week";
            var subject = ReadString(body, "subject");
            Guid? classId = null;
            if (body?.RootElement.TryGetProperty("classId", out var classEl) == true && classEl.TryGetGuid(out var cid))
            {
                classId = cid;
            }

            return Results.Json(await boards.SnapshotAsync(ws.WorkspaceId, metric, period, subject, classId, cancellationToken));
        });

        group.MapGet("/gamification/privacy", async (HttpContext http, LeaderboardService boards, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await boards.GetPrivacyAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPut("/gamification/privacy", async (HttpContext http, LeaderboardService boards, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            bool? visible = null;
            bool? showName = null;
            if (body?.RootElement.TryGetProperty("leaderboardVisible", out var v) == true && (v.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                visible = v.GetBoolean();
            }

            if (body?.RootElement.TryGetProperty("showRealName", out var n) == true && (n.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                showName = n.GetBoolean();
            }

            return Results.Json(await boards.UpdatePrivacyAsync(ws.WorkspaceId, ws.UserId, visible, showName, cancellationToken));
        });

        MapClasses(group);
        MapTeams(group);
        MapCompetitions(group);
        MapStore(group);
        MapRewards(group);
        return group;
    }

    private static void MapClasses(RouteGroupBuilder group)
    {
        group.MapGet("/classes", async (HttpContext http, AppDbContext db, ClassService classes, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            if (ws.Type != WorkspaceType.School)
            {
                return Results.Json(new { items = Array.Empty<object>(), note = "کلاس فقط در فضای مدرسه فعال است." });
            }

            return Results.Json(await classes.ListAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapPost("/classes", async (HttpContext http, AppDbContext db, ClassService classes, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacherOrAbove(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            if (ws.Type != WorkspaceType.School)
            {
                return Results.Json(new { error = "کلاس فقط در فضای مدرسه ساخته می‌شود." }, statusCode: 400);
            }

            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var name = ReadString(body, "name") ?? "";
            try
            {
                return Results.Json(await classes.CreateAsync(ws.WorkspaceId, ws.UserId, name, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/classes/{id:guid}/members", async (HttpContext http, Guid id, AppDbContext db, ClassService classes, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacherOrAbove(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("userId", out var userEl) || !userEl.TryGetInt32(out var userId))
            {
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            }

            var roleText = ReadString(body, "role") ?? "Student";
            var role = Enum.TryParse<ClassMemberRole>(roleText, true, out var parsed) ? parsed : ClassMemberRole.Student;
            try
            {
                return Results.Json(await classes.AddMemberAsync(ws.WorkspaceId, id, userId, role, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });
    }

    private static void MapTeams(RouteGroupBuilder group)
    {
        group.MapGet("/teams", async (HttpContext http, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await teams.ListAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPost("/teams", async (HttpContext http, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var name = ReadString(body, "name") ?? "";
            var description = ReadString(body, "description");
            Guid? classId = null;
            if (body?.RootElement.TryGetProperty("classId", out var classEl) == true && classEl.TryGetGuid(out var cid)) classId = cid;
            try
            {
                return Results.Json(await teams.CreateAsync(ws.WorkspaceId, ws.UserId, name, description, classId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/teams/{id:guid}/join", async (HttpContext http, Guid id, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await teams.JoinAsync(ws.WorkspaceId, ws.UserId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/teams/{id:guid}/invites", async (HttpContext http, Guid id, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("userId", out var userEl) || !userEl.TryGetInt32(out var invitedUserId))
            {
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            }

            try
            {
                return Results.Json(await teams.InviteAsync(ws.WorkspaceId, ws.UserId, id, invitedUserId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/teams/invites/{id:guid}/respond", async (HttpContext http, Guid id, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var accept = body?.RootElement.TryGetProperty("accept", out var a) == true && a.ValueKind == JsonValueKind.True;
            try
            {
                return Results.Json(await teams.RespondInviteAsync(ws.WorkspaceId, ws.UserId, id, accept, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/teams/{id:guid}/challenges", async (HttpContext http, Guid id, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var title = ReadString(body, "title") ?? "";
            var description = ReadString(body, "description");
            var targetXp = ReadInt(body, "targetXp") ?? 100;
            var startsAt = ReadDate(body, "startsAt") ?? DateTime.UtcNow;
            var endsAt = ReadDate(body, "endsAt") ?? DateTime.UtcNow.AddDays(7);
            try
            {
                return Results.Json(await teams.CreateChallengeAsync(ws.WorkspaceId, ws.UserId, id, title, description, targetXp, startsAt, endsAt, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/teams/{id:guid}", async (HttpContext http, Guid id, TeamService teams, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await teams.HistoryAsync(ws.WorkspaceId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });
    }

    private static void MapCompetitions(RouteGroupBuilder group)
    {
        group.MapGet("/competitions", async (HttpContext http, CompetitionService comps, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await comps.ListAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapPost("/competitions", async (HttpContext http, AppDbContext db, CompetitionService comps, CancellationToken cancellationToken) =>
        {
            if (await RequireCompetitionCreator(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var name = ReadString(body, "name") ?? "";
            var description = ReadString(body, "description");
            var typeText = ReadString(body, "type") ?? "Individual";
            var type = Enum.TryParse<CompetitionType>(typeText, true, out var parsed) ? parsed : CompetitionType.Individual;
            Guid? classId = null;
            if (body?.RootElement.TryGetProperty("classId", out var classEl) == true && classEl.TryGetGuid(out var cid)) classId = cid;
            var startsAt = ReadDate(body, "startsAt") ?? DateTime.UtcNow;
            var endsAt = ReadDate(body, "endsAt") ?? DateTime.UtcNow.AddDays(7);
            var scoring = body?.RootElement.TryGetProperty("scoringRules", out var rules) == true ? rules.GetRawText() : null;
            var rewards = body?.RootElement.TryGetProperty("rewards", out var rew) == true ? rew.GetRawText() : null;
            try
            {
                return Results.Json(await comps.CreateAsync(ws.WorkspaceId, ws.UserId, name, description, type, classId, startsAt, endsAt, scoring, rewards, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/competitions/{id:guid}/status", async (HttpContext http, Guid id, AppDbContext db, CompetitionService comps, CancellationToken cancellationToken) =>
        {
            if (await RequireCompetitionCreator(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var statusText = ReadString(body, "status") ?? "";
            if (!Enum.TryParse<CompetitionStatus>(statusText, true, out var status))
            {
                return Results.Json(new { error = "status نامعتبر است." }, statusCode: 400);
            }

            try
            {
                return Results.Json(await comps.UpdateStatusAsync(ws.WorkspaceId, id, status, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/competitions/{id:guid}/join", async (HttpContext http, Guid id, CompetitionService comps, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            Guid? teamId = null;
            if (body?.RootElement.TryGetProperty("teamId", out var teamEl) == true && teamEl.TryGetGuid(out var tid)) teamId = tid;
            try
            {
                return Results.Json(await comps.JoinAsync(ws.WorkspaceId, ws.UserId, id, teamId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/competitions/{id:guid}/results", async (HttpContext http, Guid id, CompetitionService comps, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await comps.ResultsAsync(ws.WorkspaceId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });
    }

    private static void MapStore(RouteGroupBuilder group)
    {
        group.MapGet("/store", async (HttpContext http, StoreService store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await store.CatalogAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapGet("/store/inventory", async (HttpContext http, StoreService store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await store.InventoryAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPost("/store/purchase", async (HttpContext http, StoreService store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("itemId", out var itemEl) || !itemEl.TryGetGuid(out var itemId))
            {
                return Results.Json(new { error = "itemId لازم است." }, statusCode: 400);
            }

            try
            {
                return Results.Json(await store.PurchaseAsync(ws.WorkspaceId, ws.UserId, itemId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/store/activate", async (HttpContext http, StoreService store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("itemId", out var itemEl) || !itemEl.TryGetGuid(out var itemId))
            {
                return Results.Json(new { error = "itemId لازم است." }, statusCode: 400);
            }

            try
            {
                return Results.Json(await store.ActivateAsync(ws.WorkspaceId, ws.UserId, itemId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/admin/store/items", async (HttpContext http, AppDbContext db, StoreService store, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolStaff(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var user = await db.Users.AsNoTracking().FirstAsync(item => item.Id == ws.UserId, cancellationToken);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var code = ReadString(body, "code") ?? "";
            var title = ReadString(body, "title") ?? "";
            var description = ReadString(body, "description");
            var categoryText = ReadString(body, "category") ?? "Accessory";
            var category = Enum.TryParse<StoreItemCategory>(categoryText, true, out var parsed) ? parsed : StoreItemCategory.Accessory;
            var price = ReadInt(body, "priceCoins") ?? 0;
            var assetKey = ReadString(body, "assetKey") ?? code;
            var cssClass = ReadString(body, "cssClass") ?? $"avatar-{code}";
            var stackable = body?.RootElement.TryGetProperty("stackable", out var st) == true && st.ValueKind == JsonValueKind.True;
            var isSystem = user.PlatformRole == 100 && body?.RootElement.TryGetProperty("isSystem", out var sys) == true && sys.ValueKind == JsonValueKind.True;
            var active = body?.RootElement.TryGetProperty("active", out var act) != true || act.ValueKind != JsonValueKind.False;
            Guid? scope = isSystem ? null : ws.WorkspaceId;
            try
            {
                return Results.Json(await store.AdminUpsertAsync(
                    scope, code, title, description, category, price, assetKey, cssClass, stackable, isSystem, active, null, null, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });
    }

    private static void MapRewards(RouteGroupBuilder group)
    {
        group.MapGet("/rewards", async (HttpContext http, AppDbContext db, RewardService rewards, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var admin = await IsSchoolStaff(http, db, cancellationToken);
            return Results.Json(await rewards.ListAsync(ws.WorkspaceId, ws.UserId, admin, cancellationToken));
        });

        group.MapPost("/rewards", async (HttpContext http, AppDbContext db, RewardService rewards, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacherOrAbove(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("userId", out var userEl) || !userEl.TryGetInt32(out var userId))
            {
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            }

            var kindText = ReadString(body, "kind") ?? "Digital";
            var kind = Enum.TryParse<RewardGrantKind>(kindText, true, out var parsed) ? parsed : RewardGrantKind.Digital;
            var title = ReadString(body, "title") ?? "";
            var description = ReadString(body, "description");
            var coins = ReadInt(body, "coinAmount");
            var xpAmount = ReadInt(body, "xpAmount");
            Guid? storeItemId = null;
            Guid? competitionId = null;
            if (body.RootElement.TryGetProperty("storeItemId", out var si) && si.TryGetGuid(out var sid)) storeItemId = sid;
            if (body.RootElement.TryGetProperty("competitionId", out var ci) && ci.TryGetGuid(out var cid)) competitionId = cid;
            try
            {
                return Results.Json(await rewards.CreateAsync(
                    ws.WorkspaceId, ws.UserId, userId, kind, title, description, coins, xpAmount, storeItemId, competitionId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/rewards/{id:guid}/approve", async (HttpContext http, Guid id, AppDbContext db, RewardService rewards, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolStaff(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var approve = body?.RootElement.TryGetProperty("approve", out var a) != true || a.ValueKind != JsonValueKind.False;
            try
            {
                return Results.Json(await rewards.ApproveAsync(ws.WorkspaceId, ws.UserId, id, approve, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });
    }

    private static bool TryParseMetric(string? metric, out LeaderboardMetric parsed)
    {
        parsed = LeaderboardMetric.WeeklyXp;
        if (string.IsNullOrWhiteSpace(metric)) return true;
        return Enum.TryParse(metric, true, out parsed);
    }

    private static string? ReadString(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static int? ReadInt(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.TryGetInt32(out var value)
            ? value
            : null;

    private static DateTime? ReadDate(JsonDocument? body, string name)
    {
        if (body is null || !body.RootElement.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind == JsonValueKind.String && DateTime.TryParse(el.GetString(), out var parsed)) return parsed.ToUniversalTime();
        if (el.TryGetDateTime(out var dt)) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }

    private static async Task<IResult?> RequireSchoolStaff(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        if (await IsSchoolStaff(http, db, cancellationToken)) return null;
        return Results.Json(new { error = "دسترسی مدیریتی نداری." }, statusCode: 403);
    }

    private static async Task<IResult?> RequireTeacherOrAbove(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return null;
        if (ws.Role is WorkspaceMemberRole.Teacher or WorkspaceMemberRole.AcademicManager
            or WorkspaceMemberRole.SchoolAdministrator or WorkspaceMemberRole.SchoolOwner)
        {
            return null;
        }

        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.WorkspaceId, cancellationToken);
        if (workspace?.OwnerUserId == ws.UserId) return null;
        return Results.Json(new { error = "دسترسی کافی نداری." }, statusCode: 403);
    }

    private static async Task<IResult?> RequireCompetitionCreator(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return null;
        if (ws.Role is WorkspaceMemberRole.SchoolOwner or WorkspaceMemberRole.SchoolAdministrator or WorkspaceMemberRole.Teacher)
        {
            return null;
        }

        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.WorkspaceId, cancellationToken);
        if (workspace?.Type == WorkspaceType.Personal && workspace.OwnerUserId == ws.UserId) return null;
        return Results.Json(new { error = "اجازه ساخت مسابقه نداری." }, statusCode: 403);
    }

    private static async Task<bool> IsSchoolStaff(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        var ws = WorkspaceAccess.Workspace(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return true;
        return ws.Role is WorkspaceMemberRole.SchoolOwner or WorkspaceMemberRole.SchoolAdministrator;
    }
}
