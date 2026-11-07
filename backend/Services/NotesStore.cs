using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Api;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public sealed class NotesStore(AppDbContext db)
{
    public const int MaxDocumentBytes = 1_000_000;
    public const int MaxImageBytes = 8_000_000;

    public async Task<JsonObject> LoadVaultAsync(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var notes = await db.Notes.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId)
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);
        var folders = await db.Folders.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId)
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);
        var trash = await db.Trash.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId)
            .OrderByDescending(item => item.DeletedAt)
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);
        var settingsRows = await db.NoteSettings.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId)
            .ToListAsync(cancellationToken);

        var settings = new JsonObject();
        foreach (var row in settingsRows)
        {
            settings[row.Key] = JsonNode.Parse(row.Value);
        }

        return new JsonObject
        {
            ["notes"] = ToArray(notes),
            ["folders"] = ToArray(folders),
            ["trash"] = ToArray(trash),
            ["settings"] = settings,
        };
    }

    public async Task UpsertNoteAsync(WorkspaceContext workspace, JsonObject note, CancellationToken cancellationToken)
    {
        var id = RequiredId(note);
        var json = note.ToJsonString();
        if (json.Length > MaxDocumentBytes) throw new InvalidOperationException("یادداشت بیش از حد بزرگ است.");
        var row = await db.Notes.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.NoteId == id,
            cancellationToken);
        if (row is null) db.Notes.Add(new NoteDocument { UserId = workspace.UserId, WorkspaceId = workspace.WorkspaceId, NoteId = id, Data = json });
        else row.Data = json;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteNoteAsync(WorkspaceContext workspace, string id, CancellationToken cancellationToken) =>
        await db.Notes
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.NoteId == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task UpsertFolderAsync(WorkspaceContext workspace, JsonObject folder, CancellationToken cancellationToken)
    {
        var id = RequiredId(folder);
        var json = folder.ToJsonString();
        if (json.Length > MaxDocumentBytes) throw new InvalidOperationException("پوشه بیش از حد بزرگ است.");
        var row = await db.Folders.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.FolderId == id,
            cancellationToken);
        if (row is null) db.Folders.Add(new FolderDocument { UserId = workspace.UserId, WorkspaceId = workspace.WorkspaceId, FolderId = id, Data = json });
        else row.Data = json;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteFolderAsync(WorkspaceContext workspace, string id, CancellationToken cancellationToken) =>
        await db.Folders
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.FolderId == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task PutSettingAsync(WorkspaceContext workspace, string key, JsonNode? value, CancellationToken cancellationToken)
    {
        if (key.Length is 0 or > 80 || key is "__proto__" or "constructor" or "prototype")
        {
            throw new InvalidOperationException("کلید تنظیم درست نیست.");
        }

        var json = value?.ToJsonString() ?? "null";
        if (json.Length > MaxDocumentBytes) throw new InvalidOperationException("تنظیم بیش از حد بزرگ است.");
        var row = await db.NoteSettings.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.Key == key,
            cancellationToken);
        if (row is null) db.NoteSettings.Add(new NoteSetting { UserId = workspace.UserId, WorkspaceId = workspace.WorkspaceId, Key = key, Value = json });
        else row.Value = json;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task PutTrashAsync(WorkspaceContext workspace, JsonObject item, CancellationToken cancellationToken)
    {
        var id = RequiredId(item);
        var deletedAt = ReadDeletedAt(item);
        var json = item.ToJsonString();
        if (json.Length > MaxDocumentBytes) throw new InvalidOperationException("مورد زباله‌دان بیش از حد بزرگ است.");
        var row = await db.Trash.FirstOrDefaultAsync(
            entry => entry.UserId == workspace.UserId && entry.WorkspaceId == workspace.WorkspaceId && entry.TrashId == id,
            cancellationToken);
        if (row is null)
        {
            db.Trash.Add(new TrashDocument
            {
                UserId = workspace.UserId,
                WorkspaceId = workspace.WorkspaceId,
                TrashId = id,
                DeletedAt = deletedAt,
                Data = json,
            });
        }
        else
        {
            row.DeletedAt = deletedAt;
            row.Data = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<JsonObject?> RestoreTrashAsync(WorkspaceContext workspace, string id, CancellationToken cancellationToken)
    {
        var row = await db.Trash.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.TrashId == id,
            cancellationToken);
        if (row is null) return null;
        if (JsonNode.Parse(row.Data) is not JsonObject item) return null;

        var kind = item["kind"]?.GetValue<string>() ?? "";
        if (item["data"] is JsonObject data)
        {
            if (kind == "note") await UpsertNoteAsync(workspace, data, cancellationToken);
            if (kind == "folder") await UpsertFolderAsync(workspace, data, cancellationToken);
        }

        db.Trash.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<int> PurgeTrashAsync(WorkspaceContext workspace, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - days * 24L * 60 * 60 * 1000;
        return await db.Trash
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.DeletedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task PutImageAsync(WorkspaceContext workspace, string id, string dataUrl, long createdAt, CancellationToken cancellationToken)
    {
        if (id.Length is 0 or > 100) throw new InvalidOperationException("شناسه تصویر درست نیست.");
        if (dataUrl.Length == 0 || dataUrl.Length > MaxImageBytes) throw new InvalidOperationException("تصویر بیش از حد بزرگ است.");
        var row = await db.NoteImages.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.ImageId == id,
            cancellationToken);
        if (row is null)
        {
            db.NoteImages.Add(new NoteImage
            {
                UserId = workspace.UserId,
                WorkspaceId = workspace.WorkspaceId,
                ImageId = id,
                DataUrl = dataUrl,
                CreatedAt = createdAt,
            });
        }
        else
        {
            row.DataUrl = dataUrl;
            row.CreatedAt = createdAt;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<NoteImage?> GetImageAsync(WorkspaceContext workspace, string id, CancellationToken cancellationToken) =>
        await db.NoteImages.AsNoTracking().FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.ImageId == id,
            cancellationToken);

    public async Task<JsonArray> ListImagesAsync(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var rows = await db.NoteImages.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId)
            .Select(item => new { item.ImageId, item.DataUrl, item.CreatedAt })
            .ToListAsync(cancellationToken);
        var images = new JsonArray();
        foreach (var row in rows)
        {
            images.Add(new JsonObject
            {
                ["id"] = row.ImageId,
                ["dataUrl"] = row.DataUrl,
                ["createdAt"] = row.CreatedAt,
            });
        }

        return images;
    }

    public async Task ReplaceVaultAsync(WorkspaceContext workspace, JsonElement vault, CancellationToken cancellationToken)
    {
        await db.Notes.Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId).ExecuteDeleteAsync(cancellationToken);
        await db.Folders.Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId).ExecuteDeleteAsync(cancellationToken);
        await db.Trash.Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId).ExecuteDeleteAsync(cancellationToken);
        await db.NoteImages.Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId).ExecuteDeleteAsync(cancellationToken);
        await db.NoteSettings.Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId).ExecuteDeleteAsync(cancellationToken);
        await MergeVaultAsync(workspace, vault, cancellationToken);
    }

    public async Task MergeVaultAsync(WorkspaceContext workspace, JsonElement vault, CancellationToken cancellationToken)
    {
        if (vault.ValueKind != JsonValueKind.Object) return;
        await UpsertMany(vault, "notes", (item, token) => UpsertNoteAsync(workspace, item, token), cancellationToken);
        await UpsertMany(vault, "folders", (item, token) => UpsertFolderAsync(workspace, item, token), cancellationToken);
        await UpsertMany(vault, "trash", (item, token) => PutTrashAsync(workspace, item, token), cancellationToken);
        if (vault.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.ValueKind != JsonValueKind.Object) continue;
                var id = image.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String
                    ? (idValue.GetString() ?? "").Trim()
                    : "";
                var dataUrl = image.TryGetProperty("dataUrl", out var urlValue) && urlValue.ValueKind == JsonValueKind.String
                    ? urlValue.GetString() ?? ""
                    : "";
                var createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (image.TryGetProperty("createdAt", out var created) && created.TryGetInt64(out var stamp)) createdAt = stamp;
                await PutImageAsync(workspace, id, dataUrl, createdAt, cancellationToken);
            }
        }

        if (vault.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in settings.EnumerateObject())
            {
                JsonNode? value = property.Value.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(property.Value.GetRawText());
                await PutSettingAsync(workspace, property.Name, value, cancellationToken);
            }
        }
    }

    private async Task UpsertMany(
        JsonElement vault,
        string name,
        Func<JsonObject, CancellationToken, Task> save,
        CancellationToken cancellationToken)
    {
        if (!vault.TryGetProperty(name, out var items) || items.ValueKind != JsonValueKind.Array) return;
        if (items.GetArrayLength() > StudyValidation.MaxPerCollection)
        {
            throw new InvalidOperationException("تعداد موردها به سقف رسیده.");
        }

        foreach (var item in items.EnumerateArray())
        {
            if (JsonNode.Parse(item.GetRawText()) is not JsonObject obj) continue;
            await save(obj, cancellationToken);
        }
    }

    private static JsonArray ToArray(IEnumerable<string> rows)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            if (JsonNode.Parse(row) is JsonNode node) array.Add(node);
        }

        return array;
    }

    private static long ReadDeletedAt(JsonObject item)
    {
        if (item["deletedAt"] is not JsonValue value) return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (value.TryGetValue<long>(out var whole)) return whole;
        if (value.TryGetValue<double>(out var number)) return (long)number;
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string RequiredId(JsonObject item)
    {
        var id = item["id"] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : "";
        if (id.Length is 0 or > 100) throw new InvalidOperationException("شناسه درست نیست.");
        return id;
    }
}
