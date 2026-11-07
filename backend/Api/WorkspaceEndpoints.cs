using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;

namespace KonkoorHelp.Api.Api;

public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaces(this RouteGroupBuilder group)
    {
        group.MapGet("/workspaces", async (HttpContext http, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            var user = WorkspaceAccess.User(http);
            var items = await workspaces.ListForUserAsync(user.Id, cancellationToken);
            return Results.Json(new
            {
                workspaces = items.Select(MapItem),
            });
        });

        group.MapPost("/workspaces/personal/ensure", async (HttpContext http, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            var context = await workspaces.EnsurePersonalAsync(WorkspaceAccess.User(http).Id, cancellationToken);
            return Results.Json(new
            {
                ok = true,
                workspace = MapContext(context),
            });
        });

        group.MapPost("/workspaces/{id:guid}/switch", async (HttpContext http, Guid id, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            try
            {
                var context = await workspaces.SwitchAsync(WorkspaceAccess.User(http).Id, id, cancellationToken);
                http.Items[WorkspaceService.ItemKey] = context;
                return Results.Json(new { ok = true, activeWorkspaceId = context.WorkspaceId });
            }
            catch (WorkspaceForbiddenException)
            {
                return Forbidden();
            }
        });

        return group;
    }

    public static object MapItem(WorkspaceListItem item) => new
    {
        id = item.Id,
        type = TypeName(item.Type),
        name = item.Name,
        role = RoleName(item.Role),
    };

    public static object MapContext(WorkspaceContext context) => new
    {
        id = context.WorkspaceId,
        type = TypeName(context.Type),
        name = context.WorkspaceName,
        role = RoleName(context.Role),
    };

    public static string TypeName(WorkspaceType type) => type switch
    {
        WorkspaceType.Personal => "personal",
        WorkspaceType.School => "school",
        _ => "unknown",
    };

    public static string RoleName(WorkspaceMemberRole role) => role switch
    {
        WorkspaceMemberRole.Student => "student",
        WorkspaceMemberRole.Parent => "parent",
        WorkspaceMemberRole.Teacher => "teacher",
        WorkspaceMemberRole.AcademicManager => "academicManager",
        WorkspaceMemberRole.SchoolAdministrator => "schoolAdministrator",
        WorkspaceMemberRole.SchoolOwner => "schoolOwner",
        _ => "unknown",
    };

    private static IResult Forbidden() =>
        Results.Json(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" }, statusCode: 403);
}
