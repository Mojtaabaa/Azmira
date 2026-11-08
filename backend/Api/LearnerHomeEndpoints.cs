using KonkoorHelp.Api.Services.Learning;

namespace KonkoorHelp.Api.Api;

public static class LearnerHomeEndpoints
{
    public static RouteGroupBuilder MapLearnerHome(this RouteGroupBuilder group)
    {
        group.MapGet("/learner/home", async (HttpContext http, LearnerHomeService home, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await home.GetHomeAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        return group;
    }
}
