using System.Text.Json;
using System.Text.RegularExpressions;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class AuthEndpoints
{
    public const int PasswordMin = 8;
    public const int PasswordMax = 128;
    private static readonly Regex UsernamePattern = new(@"^[A-Za-z0-9_.]{3,32}$", RegexOptions.Compiled);
    private const string UsernameMessage = "نام کاربری باید 3 تا 32 نویسه باشد و فقط حرف انگلیسی، عدد، نقطه یا زیرخط داشته باشد.";
    private const string PasswordMessage = "رمز عبور باید از 8 تا 128 نویسه باشد.";
    private const string LoginFailed = "نام کاربری یا رمز عبور درست نیست.";
    private const string TooMany = "تلاش‌های زیاد. چند دقیقه بعد دوباره امتحان کن.";

    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder group, AttemptLimiter loginLimiter, AttemptLimiter registerLimiter)
    {
        group.MapPost("/auth/register", async (HttpContext http, StudyStore store, PasswordService passwords, SessionService sessions, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            var (username, password) = await ReadCredentials(http, cancellationToken);
            if (!UsernamePattern.IsMatch(username)) return Error(400, UsernameMessage);
            if (password.Length < PasswordMin || password.Length > PasswordMax) return Error(400, PasswordMessage);

            var ip = ClientKey(http);
            if (registerLimiter.IsBlocked(ip)) return Error(429, TooMany);
            if (await store.FindUserAsync(username, cancellationToken) is not null) return Error(409, "این نام کاربری قبلاً گرفته شده.");

            registerLimiter.Hit(ip);
            int userId;
            try
            {
                userId = await store.CreateUserAsync(username, passwords.Hash(password), cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Error(409, "این نام کاربری قبلاً گرفته شده.");
            }

            await sessions.OpenAsync(http, userId, cancellationToken);
            var personal = await workspaces.EnsurePersonalAsync(userId, cancellationToken);
            http.Items[WorkspaceService.ItemKey] = personal;
            return Results.Json(new
            {
                user = new { id = userId, username, displayName = (string?)null, platformRole = 0 },
                personalWorkspaceId = personal.WorkspaceId,
                workspaces = new[] { WorkspaceEndpoints.MapContext(personal) },
                activeWorkspaceId = personal.WorkspaceId,
            }, statusCode: 201);
        });

        group.MapPost("/auth/login", async (HttpContext http, StudyStore store, PasswordService passwords, SessionService sessions, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            var (username, password) = await ReadCredentials(http, cancellationToken);
            if (username.Length == 0 || password.Length == 0 || password.Length > PasswordMax) return Error(400, LoginFailed);

            var key = $"{ClientKey(http)}|{username.ToLowerInvariant()}";
            if (loginLimiter.IsBlocked(key)) return Error(429, TooMany);

            var user = await store.FindUserAsync(username, cancellationToken);
            var ok = passwords.Verify(password, user?.PasswordHash);
            if (user is null || !ok)
            {
                loginLimiter.Hit(key);
                return Error(401, LoginFailed);
            }

            loginLimiter.Clear(key);
            await sessions.OpenAsync(http, user.Id, cancellationToken);
            var active = await workspaces.ResolveAsync(user.Id, null, cancellationToken);
            http.Items[WorkspaceService.ItemKey] = active;
            var list = await workspaces.ListForUserAsync(user.Id, cancellationToken);
            return Results.Json(new
            {
                user = new { id = user.Id, username = user.Username, displayName = user.DisplayName, platformRole = user.PlatformRole },
                workspaces = list.Select(WorkspaceEndpoints.MapItem),
                activeWorkspaceId = active.WorkspaceId,
                capabilities = UserCapabilities.Build(user.PlatformRole, list, active.WorkspaceId),
            });
        });

        group.MapPost("/auth/logout", async (HttpContext http, SessionService sessions, CancellationToken cancellationToken) =>
        {
            await sessions.CloseAsync(http, cancellationToken);
            return Results.Json(new { ok = true });
        });

        group.MapGet("/auth/me", async (HttpContext http, AppDbContext db, WorkspaceService workspaces, CancellationToken cancellationToken) =>
        {
            if (http.Items["user"] is not SessionUser session) return Error(401, "وارد نشده‌ای.");
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.Id, cancellationToken);
            if (user is null) return Error(401, "وارد نشده‌ای.");

            WorkspaceContext active;
            try
            {
                active = WorkspaceAccess.TryWorkspace(http)
                    ?? await workspaces.ResolveAsync(session.Id, null, cancellationToken);
            }
            catch (WorkspaceForbiddenException)
            {
                return Results.Json(new { error = "دسترسی به این فضا مجاز نیست.", code = "workspace_forbidden" }, statusCode: 403);
            }

            var list = await workspaces.ListForUserAsync(session.Id, cancellationToken);
            return Results.Json(new
            {
                user = new
                {
                    id = user.Id,
                    username = user.Username,
                    displayName = user.DisplayName,
                    platformRole = user.PlatformRole,
                },
                workspaces = list.Select(WorkspaceEndpoints.MapItem),
                activeWorkspaceId = active.WorkspaceId,
                capabilities = UserCapabilities.Build(user.PlatformRole, list, active.WorkspaceId),
            });
        });

        return group;
    }

    private static async Task<(string Username, string Password)> ReadCredentials(HttpContext http, CancellationToken cancellationToken)
    {
        using var document = await JsonDocumentSafe.Read(http, cancellationToken);
        if (document is null || document.RootElement.ValueKind != JsonValueKind.Object) return ("", "");
        var username = document.RootElement.TryGetProperty("username", out var name) && name.ValueKind == JsonValueKind.String
            ? (name.GetString() ?? "").Trim()
            : "";
        var password = document.RootElement.TryGetProperty("password", out var pass) && pass.ValueKind == JsonValueKind.String
            ? pass.GetString() ?? ""
            : "";
        return (username, password);
    }

    private static string ClientKey(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);
}
