using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Services;

namespace KonkoorHelp.Api.Api;

public static class DataEndpoints
{
    public static RouteGroupBuilder MapData(this RouteGroupBuilder group)
    {
        group.MapGet("/data", async (HttpContext http, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            return Results.Json(await store.LoadAllAsync(WorkspaceAccess.Workspace(http), cancellationToken));
        });

        group.MapPut("/data", async (HttpContext http, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("data", out var data))
            {
                return Error(400, "فایل پشتیبان باید یک شیء JSON باشد.");
            }

            try
            {
                var normalized = StudyValidation.NormalizeData(data, true);
                await store.ReplaceAllAsync(WorkspaceAccess.Workspace(http), normalized.Collections, normalized.Settings, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (StudyValidationException error)
            {
                return Error(400, error.Message);
            }
        });

        group.MapPut("/settings", async (HttpContext http, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var settingsElement = body is not null && body.RootElement.TryGetProperty("settings", out var value) ? value : default;
            try
            {
                var settings = StudyValidation.NormalizeSettings(settingsElement, true);
                await store.SaveSettingsAsync(WorkspaceAccess.Workspace(http), settings, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (StudyValidationException error)
            {
                return Error(400, error.Message);
            }
        });

        group.MapPut("/data/{collection}", async (HttpContext http, string collection, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (Unknown(collection) is { } missing) return missing;
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var items = body is not null && body.RootElement.TryGetProperty("items", out var value) ? value : default;
            try
            {
                var normalized = StudyValidation.NormalizeCollection(collection, items, true);
                await store.ReplaceCollectionAsync(WorkspaceAccess.Workspace(http), collection, normalized, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (StudyValidationException error)
            {
                return Error(400, error.Message);
            }
        });

        group.MapPost("/data/{collection}", async (HttpContext http, string collection, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (Unknown(collection) is { } missing) return missing;
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var (record, error) = ReadRecord(body?.RootElement ?? default, null);
            if (error is not null || record is null) return Error(400, error ?? "مورد باید یک شیء باشد.");

            var result = await store.AddAsync(WorkspaceAccess.Workspace(http), collection, record, cancellationToken);
            if (result == "exists") return Error(409, "شناسه تکراری است.");
            if (result == "full") return Error(413, "تعداد موردها به سقف رسیده.");
            return Results.Json(new { ok = true }, statusCode: 201);
        });

        group.MapPatch("/data/{collection}/{id}", async (HttpContext http, string collection, string id, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (Unknown(collection) is { } missing) return missing;
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var (record, error) = ReadRecord(body?.RootElement ?? default, Uri.UnescapeDataString(id));
            if (error is not null || record is null) return Error(400, error ?? "مورد باید یک شیء باشد.");

            if (!await store.SaveAsync(WorkspaceAccess.Workspace(http), collection, record, cancellationToken)) return Error(404, "این مورد پیدا نشد.");
            return Results.Json(new { ok = true });
        });

        group.MapDelete("/data/{collection}/{id}", async (HttpContext http, string collection, string id, StudyStore store, CancellationToken cancellationToken) =>
        {
            if (Unknown(collection) is { } missing) return missing;
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            if (!await store.DeleteAsync(WorkspaceAccess.Workspace(http), collection, Uri.UnescapeDataString(id), cancellationToken))
            {
                return Error(404, "این مورد پیدا نشد.");
            }

            return Results.Json(new { ok = true });
        });

        return group;
    }

    private static IResult? Unknown(string collection) =>
        StudyValidation.CollectionKeys.Contains(collection) ? null : Error(404, "این بخش وجود ندارد.");

    private static (JsonObject? Record, string? Error) ReadRecord(JsonElement body, string? forcedId)
    {
        if (body.ValueKind != JsonValueKind.Object) return (null, "مورد باید یک شیء باشد.");
        if (JsonNode.Parse(body.GetRawText()) is not JsonObject record) return (null, "مورد باید یک شیء باشد.");

        var id = forcedId ?? (record["id"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "");
        id = id.Trim();
        if (id.Length is 0 or > 100) return (null, "شناسه درست نیست.");
        record["id"] = id;
        if (record.ToJsonString().Length > StudyValidation.MaxRecordBytes) return (null, "این مورد بیش از حد بزرگ است.");
        return (record, null);
    }

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
