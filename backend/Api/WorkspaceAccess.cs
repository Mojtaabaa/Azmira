using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;

namespace KonkoorHelp.Api.Api;

public static class WorkspaceAccess
{
    public static SessionUser? TryUser(HttpContext http) =>
        http.Items["user"] as SessionUser;

    public static WorkspaceContext? TryWorkspace(HttpContext http) =>
        http.Items[WorkspaceService.ItemKey] as WorkspaceContext;

    public static IResult? RequireUser(HttpContext http) =>
        TryUser(http) is null ? Results.Json(new { error = "برای ادامه باید وارد شوی." }, statusCode: 401) : null;

    public static IResult? RequireWorkspace(HttpContext http)
    {
        if (RequireUser(http) is { } denied) return denied;
        if (TryWorkspace(http) is null)
        {
            return Results.Json(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" }, statusCode: 403);
        }

        return null;
    }

    public static WorkspaceContext Workspace(HttpContext http) =>
        TryWorkspace(http) ?? throw new InvalidOperationException("Workspace context missing.");

    public static SessionUser User(HttpContext http) =>
        TryUser(http) ?? throw new InvalidOperationException("User missing.");
}
