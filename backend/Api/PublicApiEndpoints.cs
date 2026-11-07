using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Integrations;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

/// <summary>Versioned public REST surface for third-party integrations (API key or session).</summary>
public static class PublicApiEndpoints
{
    public static RouteGroupBuilder MapPublicApiV1(this RouteGroupBuilder root)
    {
        var v1 = root.MapGroup("/v1");

        v1.MapGet("/auth/me", async (HttpContext http, AppDbContext db, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (RequirePrincipal(http) is { } denied) return denied;
            var session = PrincipalUser(http);
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.Id, cancellationToken);
            if (user is null) return Results.Json(new { error = "احراز هویت لازم است." }, statusCode: 401);
            var list = await workspaces.ListForUserAsync(user.Id, cancellationToken);
            return Results.Json(new
            {
                user = new { id = user.Id, username = user.Username, displayName = user.DisplayName, platformRole = user.PlatformRole },
                activeWorkspaceId = TryWorkspace(http)?.WorkspaceId,
                workspaces = list.Select(WorkspaceEndpoints.MapItem),
                auth = http.Items[ApiClientService.ItemKey] is ApiClient ? "api_key" : "session",
            });
        });

        v1.MapGet("/workspaces", async (HttpContext http, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (RequirePrincipal(http) is { } denied) return denied;
            if (!HasScope(http, "workspaces:read")) return ForbiddenScope();
            var list = await workspaces.ListForUserAsync(PrincipalUser(http).Id, cancellationToken);
            return Results.Json(new { items = list.Select(WorkspaceEndpoints.MapItem) });
        });

        v1.MapGet("/users", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "users:read")) return ForbiddenScope();
            var ws = Workspace(http);
            var items = await (
                from member in db.WorkspaceMembers.AsNoTracking()
                join user in db.Users.AsNoTracking() on member.UserId equals user.Id
                where member.WorkspaceId == ws.WorkspaceId && member.Status == WorkspaceMemberStatus.Active && member.DeletedAt == null
                select new
                {
                    id = user.Id,
                    username = user.Username,
                    displayName = user.DisplayName,
                    role = member.Role.ToString(),
                }).ToListAsync(cancellationToken);
            return Results.Json(new { items });
        });

        v1.MapGet("/schools/me", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "schools:read")) return ForbiddenScope();
            var ws = Workspace(http);
            try
            {
                return Results.Json(await schools.GetProfileAsync(ws.WorkspaceId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        v1.MapGet("/classes", async (HttpContext http, ClassService classes, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "classes:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await classes.ListAsync(ws.WorkspaceId, cancellationToken));
        });

        v1.MapGet("/subjects", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "subjects:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await schools.ListSubjectsAsync(ws.WorkspaceId, cancellationToken));
        });

        v1.MapGet("/study-sessions", async (HttpContext http, AppDbContext db, int? take, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "sessions:read")) return ForbiddenScope();
            var ws = Workspace(http);
            var limit = Math.Clamp(take ?? 50, 1, 200);
            var rows = await db.StudyRecords.AsNoTracking()
                .Where(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == ws.UserId && item.Collection == "sessions")
                .OrderByDescending(item => item.Seq)
                .Take(limit)
                .Select(item => item.Data)
                .ToListAsync(cancellationToken);
            var items = rows.Select(raw => JsonNode.Parse(raw)).ToList();
            return Results.Json(new { items });
        });

        v1.MapPost("/study-sessions", async (HttpContext http, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "sessions:write")) return ForbiddenScope();
            var ws = Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || body.RootElement.ValueKind != JsonValueKind.Object)
                return Results.Json(new { error = "مورد باید یک شیء باشد." }, statusCode: 400);
            if (JsonNode.Parse(body.RootElement.GetRawText()) is not JsonObject record)
                return Results.Json(new { error = "مورد باید یک شیء باشد." }, statusCode: 400);
            if (string.IsNullOrWhiteSpace(record["id"]?.GetValue<string>()))
                record["id"] = Guid.NewGuid().ToString("N");

            var result = await store.AddAsync(ws, "sessions", record, cancellationToken);
            if (result == "exists") return Results.Json(new { error = "شناسه تکراری است." }, statusCode: 409);
            if (result == "full") return Results.Json(new { error = "تعداد موردها به سقف رسیده." }, statusCode: 413);
            return Results.Json(new { ok = true, id = record["id"]?.GetValue<string>() }, statusCode: 201);
        });

        v1.MapGet("/study-plans", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "plans:read")) return ForbiddenScope();
            var ws = Workspace(http);
            var items = await db.AiPlanSuggestions.AsNoTracking()
                .Where(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == ws.UserId)
                .OrderByDescending(item => item.CreatedAt)
                .Take(50)
                .Select(item => new
                {
                    id = item.Id,
                    title = item.Title,
                    status = item.Status.ToString(),
                    createdAt = item.CreatedAt,
                    resolvedAt = item.ResolvedAt,
                })
                .ToListAsync(cancellationToken);
            return Results.Json(new { items });
        });

        v1.MapGet("/assessments", async (HttpContext http, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "assessments:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await assessments.ListAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        v1.MapGet("/assessments/{id:guid}", async (HttpContext http, Guid id, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "assessments:read")) return ForbiddenScope();
            var ws = Workspace(http);
            try
            {
                return Results.Json(await assessments.GetAsync(ws.WorkspaceId, id, includeAnswers: false, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        v1.MapGet("/analytics/me", async (HttpContext http, GamificationQueryService query, MasteryService mastery, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "analytics:read")) return ForbiddenScope();
            var ws = Workspace(http);
            var profile = await query.GetProfileAsync(ws.WorkspaceId, ws.UserId, cancellationToken);
            var masteryMap = await mastery.GetMapAsync(ws.WorkspaceId, ws.UserId, null, cancellationToken);
            return Results.Json(new { profile, mastery = masteryMap });
        });

        v1.MapGet("/analytics/school", async (
            HttpContext http,
            SchoolAnalyticsService analytics,
            Guid? classId,
            Guid? academicYearId,
            CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "analytics:read")) return ForbiddenScope();
            var ws = Workspace(http);
            try
            {
                return Results.Json(await analytics.DashboardAsync(
                    ws.WorkspaceId,
                    ws.Role,
                    ws.UserId,
                    academicYearId,
                    null,
                    classId,
                    null,
                    null,
                    null,
                    cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        v1.MapGet("/gamification/me", async (HttpContext http, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "gamification:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await query.GetProfileAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        v1.MapGet("/quests", async (HttpContext http, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "quests:read")) return ForbiddenScope();
            var ws = Workspace(http);
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
                        progress = row?.Progress ?? 0,
                        targetValue = quest.TargetValue,
                        status = (row?.Status ?? QuestStatus.Active).ToString(),
                        rewardXp = quest.RewardXp,
                    };
                }),
            });
        });

        v1.MapGet("/achievements", async (HttpContext http, AppDbContext db, GamificationProfileService profiles, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "achievements:read")) return ForbiddenScope();
            var ws = Workspace(http);
            var profile = await profiles.ResolveAsync(ws.WorkspaceId, cancellationToken);
            var badges = await db.Badges.AsNoTracking()
                .Where(item => item.ProfileId == profile.Id || item.WorkspaceId == ws.WorkspaceId)
                .ToListAsync(cancellationToken);
            var owned = await db.UserBadges.AsNoTracking()
                .Where(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == ws.UserId)
                .Select(item => item.BadgeId)
                .ToListAsync(cancellationToken);
            var ownedSet = owned.ToHashSet();
            return Results.Json(new
            {
                items = badges.Select(badge => new
                {
                    id = badge.Id,
                    code = badge.Code,
                    title = badge.IsHidden && !ownedSet.Contains(badge.Id) ? "نشان مخفی" : badge.Title,
                    earned = ownedSet.Contains(badge.Id),
                    tier = badge.Tier.ToString(),
                }),
            });
        });

        v1.MapGet("/leaderboards", async (
            HttpContext http,
            LeaderboardService boards,
            string? metric,
            string? period,
            Guid? classId,
            CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "leaderboards:read")) return ForbiddenScope();
            var ws = Workspace(http);
            if (!Enum.TryParse<LeaderboardMetric>(metric ?? "Xp", true, out var parsed))
                return Results.Json(new { error = "metric نامعتبر است." }, statusCode: 400);
            return Results.Json(await boards.GetBoardAsync(
                ws.WorkspaceId,
                parsed,
                period ?? "week",
                null,
                classId,
                1,
                20,
                cancellationToken));
        });

        v1.MapGet("/rewards", async (HttpContext http, RewardService rewards, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "rewards:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await rewards.ListAsync(ws.WorkspaceId, ws.UserId, adminView: false, cancellationToken));
        });

        v1.MapGet("/notifications", async (HttpContext http, GamificationQueryService query, CancellationToken cancellationToken) =>
        {
            if (RequireWorkspace(http) is { } denied) return denied;
            if (!HasScope(http, "notifications:read")) return ForbiddenScope();
            var ws = Workspace(http);
            return Results.Json(await query.GetNotificationsAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        return v1;
    }

    private static IResult? RequirePrincipal(HttpContext http) =>
        http.Items["user"] is SessionUser ? null : Results.Json(new { error = "احراز هویت لازم است." }, statusCode: 401);

    private static IResult? RequireWorkspace(HttpContext http)
    {
        if (RequirePrincipal(http) is { } denied) return denied;
        if (TryWorkspace(http) is null)
            return Results.Json(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" }, statusCode: 403);
        return null;
    }

    private static SessionUser PrincipalUser(HttpContext http) =>
        (SessionUser)http.Items["user"]!;

    private static WorkspaceContext? TryWorkspace(HttpContext http) =>
        http.Items[WorkspaceService.ItemKey] as WorkspaceContext;

    private static WorkspaceContext Workspace(HttpContext http) =>
        TryWorkspace(http) ?? throw new InvalidOperationException("Workspace context missing.");

    private static bool HasScope(HttpContext http, string scope)
    {
        if (http.Items[ApiClientService.ItemKey] is not ApiClient client) return true;
        return ApiClientService.HasScope(client, scope) || ApiClientService.HasScope(client, "*");
    }

    private static IResult ForbiddenScope() =>
        Results.Json(new { error = "این کلید API مجوز لازم را ندارد.", code = "scope_forbidden" }, statusCode: 403);
}
