using System.Text.Json;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;

namespace KonkoorHelp.Api.Api;

public static class ImprovementEndpoints
{
    public static RouteGroupBuilder MapPersonalImprovement(this RouteGroupBuilder group)
    {
        group.MapGet("/improvement/me", async (HttpContext http, PersonalImprovementService improvement, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await improvement.GetMeAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapPost("/improvement/rebaseline", async (HttpContext http, PersonalImprovementService improvement, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var consent = body?.RootElement.TryGetProperty("consent", out var c) == true && c.ValueKind == JsonValueKind.True;
            string? subject = null;
            if (body?.RootElement.TryGetProperty("subjectKey", out var s) == true && s.ValueKind == JsonValueKind.String)
                subject = s.GetString();
            try
            {
                return Results.Json(await improvement.RebaselineAsync(ws.WorkspaceId, ws.UserId, consent, subject, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/coach/chat", async (HttpContext http, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var message = body?.RootElement.TryGetProperty("message", out var m) == true && m.ValueKind == JsonValueKind.String
                ? m.GetString()
                : null;
            Guid? threadId = null;
            if (body?.RootElement.TryGetProperty("threadId", out var t) == true && t.TryGetGuid(out var tid))
                threadId = tid;
            try
            {
                return Results.Json(await coach.ChatAsync(ws.WorkspaceId, ws.UserId, message ?? "", threadId, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        return group;
    }
}
