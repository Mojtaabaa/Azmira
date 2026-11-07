using System.Text.Json;
using KonkoorHelp.Api.Services;

namespace KonkoorHelp.Api.Api;

public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccount(this RouteGroupBuilder group)
    {
        group.MapGet("/account/export", async (HttpContext http, AccountService account, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            return Results.Json(await account.ExportAsync(WorkspaceAccess.Workspace(http), cancellationToken));
        });

        group.MapPost("/account/import", async (HttpContext http, AccountService account, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var document = await JsonDocumentSafe.Read(http, cancellationToken);
            if (document is null || document.RootElement.ValueKind != JsonValueKind.Object) return Error(400, "فایل پشتیبان باید یک شیء JSON باشد.");
            if (!document.RootElement.TryGetProperty("backup", out var backup) || backup.ValueKind != JsonValueKind.Object)
            {
                return Error(400, "فایل پشتیبان باید یک شیء JSON باشد.");
            }

            var mode = document.RootElement.TryGetProperty("mode", out var modeValue) && modeValue.ValueKind == JsonValueKind.String
                ? modeValue.GetString() ?? ""
                : "";
            try
            {
                await account.ImportAsync(WorkspaceAccess.Workspace(http), backup, mode, cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (StudyValidationException error)
            {
                return Error(400, error.Message);
            }
            catch (InvalidOperationException error)
            {
                return Error(400, error.Message);
            }
        });

        return group;
    }

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
