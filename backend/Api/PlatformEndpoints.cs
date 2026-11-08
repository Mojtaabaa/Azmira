using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class PlatformEndpoints
{
    public static RouteGroupBuilder MapPlatformAdmin(this RouteGroupBuilder group)
    {
        group.MapGet("/platform/dashboard", async (HttpContext http, AppDbContext db, PlatformAdminService platform, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            return Results.Json(await platform.DashboardAsync(cancellationToken));
        });

        group.MapGet("/platform/users", async (HttpContext http, AppDbContext db, PlatformAdminService platform, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            var query = http.Request.Query["q"].ToString();
            var take = int.TryParse(http.Request.Query["take"], out var t) ? t : 50;
            return Results.Json(await platform.ListUsersAsync(query, take, cancellationToken));
        });

        group.MapPost("/platform/users/{userId:int}/role", async (HttpContext http, int userId, AppDbContext db, PlatformAdminService platform, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            var actor = WorkspaceAccess.User(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            byte role = 0;
            if (body is not null && body.RootElement.TryGetProperty("platformRole", out var el))
            {
                if (el.TryGetByte(out var b)) role = b;
                else if (el.TryGetInt32(out var i)) role = (byte)Math.Clamp(i, 0, 255);
            }

            try
            {
                return Results.Json(await platform.SetPlatformRoleAsync(actor.Id, userId, role, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/platform/school-requests", async (HttpContext http, AppDbContext db, SchoolRegistrationService registrations, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            return Results.Json(await registrations.ListPendingAsync(cancellationToken));
        });

        group.MapPost("/platform/school-requests/{id:guid}/approve", async (HttpContext http, Guid id, AppDbContext db, SchoolRegistrationService registrations, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            var actor = WorkspaceAccess.User(http);
            try
            {
                return Results.Json(await registrations.ApproveAsync(id, actor.Id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/platform/school-requests/{id:guid}/reject", async (HttpContext http, Guid id, AppDbContext db, SchoolRegistrationService registrations, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            var actor = WorkspaceAccess.User(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var notes = body is not null
                && body.RootElement.TryGetProperty("notes", out var n)
                && n.ValueKind == JsonValueKind.String
                    ? n.GetString()
                    : null;
            try
            {
                return Results.Json(await registrations.RejectAsync(id, actor.Id, notes, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/platform/schools", async (HttpContext http, AppDbContext db, PlatformAdminService platform, CancellationToken cancellationToken) =>
        {
            if (await RequirePlatformAdmin(http, db, cancellationToken) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null
                || !body.RootElement.TryGetProperty("ownerUserId", out var ownerEl)
                || !ownerEl.TryGetInt32(out var ownerUserId))
            {
                return Results.Json(new { error = "ownerUserId لازم است." }, statusCode: 400);
            }

            var name = ReadString(body, "name") ?? "";
            var city = ReadString(body, "city");
            var description = ReadString(body, "description");
            try
            {
                return Results.Json(await platform.CreateSchoolForOwnerAsync(ownerUserId, name, city, description, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        return group;
    }

    public static async Task<IResult?> RequirePlatformAdmin(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
        var session = WorkspaceAccess.User(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.Id, cancellationToken);
        if (!PlatformRoles.IsAdministrator(user))
            return Results.Json(new { error = "فقط ادمین سامانه.", code = "platform_admin_required" }, statusCode: 403);
        return null;
    }

    private static string? ReadString(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
