using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;

namespace KonkoorHelp.Api.Api;

public static class NotesEndpoints
{
    public static RouteGroupBuilder MapNotes(this RouteGroupBuilder group)
    {
        group.MapGet("/notes/vault", async (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            return Results.Json(await store.LoadVaultAsync(WorkspaceAccess.Workspace(http), cancellationToken));
        });

        group.MapPut("/notes/notes", (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
            Upsert(http, store, static (ws, body, token, notes) => notes.UpsertNoteAsync(ws, body, token), cancellationToken));

        group.MapDelete("/notes/notes/{id}", async (HttpContext http, string id, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            await store.DeleteNoteAsync(WorkspaceAccess.Workspace(http), Uri.UnescapeDataString(id), cancellationToken);
            return Results.Json(new { ok = true });
        });

        group.MapPut("/notes/folders", (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
            Upsert(http, store, static (ws, body, token, notes) => notes.UpsertFolderAsync(ws, body, token), cancellationToken));

        group.MapDelete("/notes/folders/{id}", async (HttpContext http, string id, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            await store.DeleteFolderAsync(WorkspaceAccess.Workspace(http), Uri.UnescapeDataString(id), cancellationToken);
            return Results.Json(new { ok = true });
        });

        group.MapPut("/notes/settings", async (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await ReadObject(http, cancellationToken);
            if (body is null) return Error(400, "تنظیم باید یک شیء باشد.");
            var key = body["key"] is JsonValue keyValue && keyValue.TryGetValue<string>(out var text) ? text.Trim() : "";
            try
            {
                await store.PutSettingAsync(WorkspaceAccess.Workspace(http), key, body["value"], cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (InvalidOperationException error)
            {
                return Error(400, error.Message);
            }
        });

        group.MapPut("/notes/trash", (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
            Upsert(http, store, static (ws, body, token, notes) => notes.PutTrashAsync(ws, body, token), cancellationToken));

        group.MapPost("/notes/trash/{id}/restore", async (HttpContext http, string id, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var item = await store.RestoreTrashAsync(WorkspaceAccess.Workspace(http), Uri.UnescapeDataString(id), cancellationToken);
            if (item is null) return Error(404, "این مورد پیدا نشد.");
            return Results.Json(item);
        });

        group.MapDelete("/notes/trash", async (HttpContext http, int? days, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var span = days is null or < 1 ? 30 : days.Value;
            await store.PurgeTrashAsync(WorkspaceAccess.Workspace(http), span, cancellationToken);
            return Results.Json(new { ok = true });
        });

        group.MapPut("/notes/images", async (HttpContext http, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await ReadObject(http, cancellationToken);
            if (body is null) return Error(400, "تصویر باید یک شیء باشد.");
            var id = body["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var text) ? text.Trim() : "";
            var dataUrl = body["dataUrl"] is JsonValue dataValue && dataValue.TryGetValue<string>(out var url) ? url : "";
            var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (body["createdAt"] is JsonValue created && created.TryGetValue<long>(out var stamp)) createdAt = stamp;
            try
            {
                await store.PutImageAsync(WorkspaceAccess.Workspace(http), id, dataUrl, createdAt, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (InvalidOperationException error)
            {
                return Error(400, error.Message);
            }
        });

        group.MapGet("/notes/images/{id}", async (HttpContext http, string id, NotesStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var image = await store.GetImageAsync(WorkspaceAccess.Workspace(http), Uri.UnescapeDataString(id), cancellationToken);
            if (image is null) return Error(404, "این تصویر پیدا نشد.");
            return Results.Json(new { id = image.ImageId, dataUrl = image.DataUrl, createdAt = image.CreatedAt });
        });

        return group;
    }

    private static async Task<IResult> Upsert(
        HttpContext http,
        NotesStore store,
        Func<WorkspaceContext, JsonObject, CancellationToken, NotesStore, Task> save,
        CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var body = await ReadObject(http, cancellationToken);
        if (body is null) return Error(400, "مورد باید یک شیء باشد.");
        try
        {
            await save(WorkspaceAccess.Workspace(http), body, cancellationToken, store);
            return Results.Json(new { ok = true });
        }
        catch (InvalidOperationException error)
        {
            return Error(400, error.Message);
        }
    }

    private static async Task<JsonObject?> ReadObject(HttpContext http, CancellationToken cancellationToken)
    {
        var document = await JsonDocumentSafe.Read(http, cancellationToken);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object) return null;
        return JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject;
    }

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
