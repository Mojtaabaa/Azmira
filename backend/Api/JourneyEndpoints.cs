using KonkoorHelp.Api.Services.Learning;

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

        return group;
    }
}
