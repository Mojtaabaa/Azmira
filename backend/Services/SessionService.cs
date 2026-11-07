using System.Security.Cryptography;
using System.Text;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services;

public sealed record SessionUser(int Id, string Username);

public sealed class SessionService(AppDbContext db)
{
    public const string CookieName = "kh_session";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public async Task<string> OpenAsync(HttpContext http, int userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await db.AuthSessions.Where(session => session.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        db.AuthSessions.Add(new AuthSession
        {
            TokenHash = Hash(token),
            UserId = userId,
            ExpiresAt = now + (long)Lifetime.TotalMilliseconds,
        });
        await db.SaveChangesAsync(cancellationToken);

        http.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = Lifetime,
            Secure = http.Request.IsHttps,
            IsEssential = true,
        });

        return token;
    }

    public async Task<SessionUser?> FindAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token);
        var row = await db.AuthSessions
            .AsNoTracking()
            .Where(session => session.TokenHash == hash)
            .Select(session => new { session.ExpiresAt, session.User.Id, session.User.Username })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null) return null;
        if (row.ExpiresAt < DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            await db.AuthSessions.Where(session => session.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);
            return null;
        }

        return new SessionUser(row.Id, row.Username);
    }

    public async Task CloseAsync(HttpContext http, CancellationToken cancellationToken)
    {
        if (http.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            var hash = Hash(token);
            await db.AuthSessions.Where(session => session.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);
        }

        http.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Secure = http.Request.IsHttps,
        });
    }

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
