using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Api;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public sealed class StudyStore(AppDbContext db, WorkspaceService workspaces, StudyEventEmitter events)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<int> CreateUserAsync(string username, string passwordHash, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new UserAccount
        {
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        db.Settings.Add(new UserSettings
        {
            UserId = user.Id,
            Data = StudyValidation.SerializeSettings(StudyValidation.DefaultSettings()),
        });
        await db.SaveChangesAsync(cancellationToken);
        await workspaces.EnsurePersonalAsync(user.Id, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return user.Id;
    }

    public Task<UserAccount?> FindUserAsync(string username, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(user => user.NormalizedUsername == username.ToLowerInvariant(), cancellationToken);

    public async Task<JsonObject> LoadAllAsync(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var data = new JsonObject();
        foreach (var key in StudyValidation.CollectionKeys) data[key] = new JsonArray();

        var rows = await db.StudyRecords
            .AsNoTracking()
            .Where(record => record.UserId == workspace.UserId && record.WorkspaceId == workspace.WorkspaceId)
            .OrderBy(record => record.Seq)
            .Select(record => new { record.Collection, record.Data })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (data[row.Collection] is not JsonArray list) continue;
            if (JsonNode.Parse(row.Data) is JsonObject item) list.Add(item);
        }

        data["settings"] = JsonSerializer.SerializeToNode(await LoadSettingsAsync(workspace, cancellationToken), Json);
        return data;
    }

    public async Task<SettingsModel> LoadSettingsAsync(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var workspaceSettings = await db.WorkspaceSettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.WorkspaceId)
            .Select(item => item.Data)
            .FirstOrDefaultAsync(cancellationToken);

        if (workspaceSettings is not null)
        {
            return StudyValidation.NormalizeSettings(StudyValidation.ParseStored(workspaceSettings), false);
        }

        var saved = await db.Settings.AsNoTracking()
            .Where(item => item.UserId == workspace.UserId)
            .Select(item => item.Data)
            .FirstOrDefaultAsync(cancellationToken);
        return saved is null
            ? StudyValidation.DefaultSettings()
            : StudyValidation.NormalizeSettings(StudyValidation.ParseStored(saved), false);
    }

    public async Task<string> AddAsync(WorkspaceContext workspace, string collection, JsonObject record, CancellationToken cancellationToken)
    {
        var count = await db.StudyRecords.CountAsync(
            item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.Collection == collection,
            cancellationToken);
        if (count >= StudyValidation.MaxPerCollection) return "full";

        var id = record["id"]?.GetValue<string>() ?? "";
        db.StudyRecords.Add(new StudyRecord
        {
            UserId = workspace.UserId,
            WorkspaceId = workspace.WorkspaceId,
            Collection = collection,
            ItemId = id,
            Data = record.ToJsonString(),
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await events.EmitForWriteAsync(workspace, collection, record, emitEvents: true, cancellationToken);
            return "ok";
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return "exists";
        }
    }

    public async Task<bool> SaveAsync(WorkspaceContext workspace, string collection, JsonObject record, CancellationToken cancellationToken)
    {
        var id = record["id"]?.GetValue<string>() ?? "";
        var row = await db.StudyRecords.FirstOrDefaultAsync(
            item => item.UserId == workspace.UserId
                && item.WorkspaceId == workspace.WorkspaceId
                && item.Collection == collection
                && item.ItemId == id,
            cancellationToken);
        if (row is null) return false;
        row.Data = record.ToJsonString();
        await db.SaveChangesAsync(cancellationToken);
        await events.EmitForWriteAsync(workspace, collection, record, emitEvents: true, cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(WorkspaceContext workspace, string collection, string id, CancellationToken cancellationToken)
    {
        var deleted = await db.StudyRecords
            .Where(item => item.UserId == workspace.UserId
                && item.WorkspaceId == workspace.WorkspaceId
                && item.Collection == collection
                && item.ItemId == id)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task ReplaceCollectionAsync(WorkspaceContext workspace, string collection, IReadOnlyList<JsonObject> items, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.StudyRecords
            .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.Collection == collection)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var item in items)
        {
            db.StudyRecords.Add(new StudyRecord
            {
                UserId = workspace.UserId,
                WorkspaceId = workspace.WorkspaceId,
                Collection = collection,
                ItemId = item["id"]?.GetValue<string>() ?? "",
                Data = item.ToJsonString(),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task ReplaceAllAsync(WorkspaceContext workspace, Dictionary<string, List<JsonObject>> collections, SettingsModel settings, CancellationToken cancellationToken)
    {
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            foreach (var collection in StudyValidation.CollectionKeys)
            {
                await db.StudyRecords
                    .Where(item => item.UserId == workspace.UserId && item.WorkspaceId == workspace.WorkspaceId && item.Collection == collection)
                    .ExecuteDeleteAsync(cancellationToken);
                foreach (var item in collections[collection])
                {
                    db.StudyRecords.Add(new StudyRecord
                    {
                        UserId = workspace.UserId,
                        WorkspaceId = workspace.WorkspaceId,
                        Collection = collection,
                        ItemId = item["id"]?.GetValue<string>() ?? "",
                        Data = item.ToJsonString(),
                    });
                }
            }

            await SaveSettingsAsync(workspace, settings, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task MergeAllAsync(WorkspaceContext workspace, Dictionary<string, List<JsonObject>> collections, SettingsModel settings, CancellationToken cancellationToken)
    {
        foreach (var collection in StudyValidation.CollectionKeys)
        {
            foreach (var item in collections[collection])
            {
                var id = item["id"]?.GetValue<string>() ?? "";
                var row = await db.StudyRecords.FirstOrDefaultAsync(
                    record => record.UserId == workspace.UserId
                        && record.WorkspaceId == workspace.WorkspaceId
                        && record.Collection == collection
                        && record.ItemId == id,
                    cancellationToken);
                var json = item.ToJsonString();
                if (row is null)
                {
                    db.StudyRecords.Add(new StudyRecord
                    {
                        UserId = workspace.UserId,
                        WorkspaceId = workspace.WorkspaceId,
                        Collection = collection,
                        ItemId = id,
                        Data = json,
                    });
                }
                else
                {
                    row.Data = json;
                }
            }
        }

        await SaveSettingsAsync(workspace, settings, cancellationToken);
    }

    public async Task SaveSettingsAsync(WorkspaceContext workspace, SettingsModel settings, CancellationToken cancellationToken)
    {
        var json = StudyValidation.SerializeSettings(settings);
        var workspaceRow = await db.WorkspaceSettings.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspace.WorkspaceId,
            cancellationToken);
        if (workspaceRow is null)
        {
            db.WorkspaceSettings.Add(new WorkspaceSettings
            {
                WorkspaceId = workspace.WorkspaceId,
                Data = json,
                LocaleDefault = "fa-IR",
                TimeZone = "Asia/Tehran",
                FeatureFlags = "{}",
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            workspaceRow.Data = json;
            workspaceRow.UpdatedAt = DateTime.UtcNow;
        }

        if (workspace.Type == WorkspaceType.Personal)
        {
            var row = await db.Settings.FirstOrDefaultAsync(item => item.UserId == workspace.UserId, cancellationToken);
            if (row is null) db.Settings.Add(new UserSettings { UserId = workspace.UserId, Data = json });
            else row.Data = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
