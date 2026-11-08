using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class JourneyEndpoints
{
    public static RouteGroupBuilder MapLearningJourney(this RouteGroupBuilder group)
    {
        group.MapGet("/journey/me", async (HttpContext http, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await journeys.GetMeAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPost("/journey/evaluate-unlock", async (HttpContext http, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await journeys.EvaluateAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapGet("/journey/packs", async (HttpContext http, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolAuthor(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await journeys.ListPacksAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapPost("/journey/packs", async (HttpContext http, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolAuthor(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            try
            {
                var regions = ParseRegions(body);
                return Results.Json(await journeys.CreatePackAsync(
                    ws.WorkspaceId,
                    ReadString(body, "code") ?? "",
                    ReadString(body, "title") ?? "",
                    ReadString(body, "locale"),
                    regions,
                    cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/journey/packs/{id:guid}/publish", async (HttpContext http, Guid id, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolAuthor(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await journeys.PublishPackAsync(ws.WorkspaceId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/journey/packs/{id:guid}/activate", async (HttpContext http, Guid id, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolAuthor(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await journeys.ActivatePackAsync(ws.WorkspaceId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/journey/packs/deactivate", async (HttpContext http, JourneyService journeys, CancellationToken cancellationToken) =>
        {
            if (await RequireSchoolAuthor(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await journeys.ActivatePackAsync(ws.WorkspaceId, null, cancellationToken));
        });

        return group;
    }

    private static async Task<IResult?> RequireSchoolAuthor(HttpContext http, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        if (ws.Type != WorkspaceType.School)
            return Results.Json(new { error = "بسته‌های سفر فقط در فضای مدرسه قابل تألیف‌اند." }, statusCode: 400);
        if (SchoolAccess.IsTeacherOrAbove(ws.Role) || SchoolAccess.IsAdmin(ws.Role))
            return null;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ws.UserId, cancellationToken);
        if (PlatformRoles.IsAdministrator(user)) return null;
        return Results.Json(new { error = "دسترسی معلم/مدیر برای تألیف بسته لازم است." }, statusCode: 403);
    }

    private static List<PackRegionInput> ParseRegions(JsonDocument? body)
    {
        var list = new List<PackRegionInput>();
        if (body?.RootElement.TryGetProperty("regions", out var regions) != true || regions.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var regionEl in regions.EnumerateArray())
        {
            var region = new PackRegionInput
            {
                Title = regionEl.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null,
                SubjectKey = regionEl.TryGetProperty("subjectKey", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null,
                VisualThemeKey = regionEl.TryGetProperty("visualThemeKey", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null,
            };
            if (regionEl.TryGetProperty("stages", out var stages) && stages.ValueKind == JsonValueKind.Array)
            {
                foreach (var stageEl in stages.EnumerateArray())
                {
                    region.Stages.Add(new PackStageInput
                    {
                        Title = stageEl.TryGetProperty("title", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null,
                        Topic = stageEl.TryGetProperty("topic", out var tp) && tp.ValueKind == JsonValueKind.String ? tp.GetString() : null,
                        NarrativeBlurb = stageEl.TryGetProperty("narrativeBlurb", out var nb) && nb.ValueKind == JsonValueKind.String ? nb.GetString() : null,
                        AlwaysOpen = stageEl.TryGetProperty("alwaysOpen", out var ao) && ao.ValueKind == JsonValueKind.True,
                        MinMasteryLevel = stageEl.TryGetProperty("minMasteryLevel", out var ml) && ml.TryGetInt32(out var mlv) ? mlv : null,
                        MinScoreEma = stageEl.TryGetProperty("minScoreEma", out var ema) && ema.TryGetDouble(out var emav) ? emav : null,
                        MinAssessmentPercent = stageEl.TryGetProperty("minAssessmentPercent", out var ap) && ap.TryGetDouble(out var apv) ? apv : null,
                        MinSrsCorrectReviews = stageEl.TryGetProperty("minSrsCorrectReviews", out var sr) && sr.TryGetInt32(out var srv) ? srv : null,
                        Coins = stageEl.TryGetProperty("coins", out var c) && c.TryGetInt32(out var cv) ? cv : null,
                    });
                }
            }

            list.Add(region);
        }

        return list;
    }

    private static string? ReadString(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
