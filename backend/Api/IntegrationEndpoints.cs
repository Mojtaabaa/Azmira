using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class IntegrationEndpoints
{
    public static RouteGroupBuilder MapIntegrations(this RouteGroupBuilder group)
    {
        group.MapGet("/integrations/api-clients", async (HttpContext http, ApiClientService clients, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            var user = WorkspaceAccess.User(http);
            var ws = WorkspaceAccess.TryWorkspace(http);
            return Results.Json(await clients.ListAsync(user.Id, ws?.WorkspaceId, cancellationToken));
        });

        group.MapPost("/integrations/api-clients", async (HttpContext http, AppDbContext db, ApiClientService clients, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var session = WorkspaceAccess.User(http);
            var ws = WorkspaceAccess.Workspace(http);
            var account = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.Id, cancellationToken);
            if (account is null) return Results.Json(new { error = "احراز هویت لازم است." }, statusCode: 401);

            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null) return Results.Json(new { error = "بدنه نامعتبر است." }, statusCode: 400);
            var name = body.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
            var scopes = ReadStringArray(body.RootElement, "scopes");
            try
            {
                return Results.Json(
                    await clients.CreateAsync(account.Id, account.PlatformRole, name, ws.WorkspaceId, scopes, cancellationToken),
                    statusCode: 201);
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

        group.MapPost("/integrations/api-clients/{id:guid}/revoke", async (HttpContext http, Guid id, ApiClientService clients, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            try
            {
                return Results.Json(await clients.RevokeAsync(WorkspaceAccess.User(http).Id, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        group.MapGet("/integrations/webhooks", async (HttpContext http, WebhookService webhooks, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await webhooks.ListEndpointsAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        group.MapPost("/integrations/webhooks", async (HttpContext http, WebhookService webhooks, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null) return Results.Json(new { error = "بدنه نامعتبر است." }, statusCode: 400);
            var url = body.RootElement.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? "" : "";
            var events = ReadStringArray(body.RootElement, "eventTypes");
            try
            {
                return Results.Json(await webhooks.CreateEndpointAsync(ws.WorkspaceId, ws.UserId, url, events, cancellationToken), statusCode: 201);
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

        group.MapPost("/integrations/webhooks/{id:guid}/deactivate", async (HttpContext http, Guid id, WebhookService webhooks, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await webhooks.DeactivateEndpointAsync(ws.WorkspaceId, ws.UserId, id, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        group.MapGet("/integrations/webhooks/deliveries", async (HttpContext http, WebhookService webhooks, Guid? endpointId, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await webhooks.ListDeliveriesAsync(ws.WorkspaceId, ws.UserId, endpointId, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        return group;
    }

    private static string[] ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return el.EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToArray();
    }
}
