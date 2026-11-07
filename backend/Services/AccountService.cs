using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Api;
using KonkoorHelp.Api.Data;

namespace KonkoorHelp.Api.Services;

public sealed class AccountService(AppDbContext db, StudyStore study, NotesStore notes)
{
    public async Task<JsonObject> ExportAsync(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var data = await study.LoadAllAsync(workspace, cancellationToken);
        var vault = await notes.LoadVaultAsync(workspace, cancellationToken);
        vault["images"] = await notes.ListImagesAsync(workspace, cancellationToken);
        return new JsonObject
        {
            ["kind"] = "konkoor-help",
            ["version"] = 2,
            ["exportedAt"] = DateTime.UtcNow.ToString("O"),
            ["workspaceId"] = workspace.WorkspaceId.ToString(),
            ["data"] = data,
            ["vault"] = vault,
        };
    }

    public async Task ImportAsync(WorkspaceContext workspace, JsonElement backup, string mode, CancellationToken cancellationToken)
    {
        if (mode is not ("replace" or "merge")) throw new InvalidOperationException("حالت وارد کردن درست نیست.");
        var dataElement = backup.TryGetProperty("data", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object
            ? wrapped
            : backup;
        var normalized = StudyValidation.NormalizeData(dataElement, true);
        var hasVault = backup.TryGetProperty("vault", out var vault) && vault.ValueKind == JsonValueKind.Object;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (mode == "merge") await study.MergeAllAsync(workspace, normalized.Collections, normalized.Settings, cancellationToken);
        else await study.ReplaceAllAsync(workspace, normalized.Collections, normalized.Settings, cancellationToken);

        if (hasVault)
        {
            if (mode == "merge") await notes.MergeVaultAsync(workspace, vault, cancellationToken);
            else await notes.ReplaceVaultAsync(workspace, vault, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }
}
