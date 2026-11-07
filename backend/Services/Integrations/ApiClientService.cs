using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Integrations;

public sealed class ApiClientService(AppDbContext db)
{
    public const string ItemKey = "apiClient";
    public const string HeaderName = "X-Api-Key";

    public static readonly string[] SuggestedLeastPrivilegeScopes =
    [
        "workspaces:read",
        "sessions:read",
        "gamification:read",
    ];

    public async Task<object> CreateAsync(
        int ownerUserId,
        byte platformRole,
        string name,
        Guid workspaceId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        await IntegrationAccess.EnsureCanManageIntegrationsAsync(db, workspaceId, ownerUserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("نام کلاینت لازم است.");

        var normalized = scopes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
            throw new InvalidOperationException("حداقل یک scope صریح لازم است. مقدار پیش‌فرض * دیگر اعمال نمی‌شود.");

        if (normalized.Any(item => item == "*") && !IntegrationAccess.IsPlatformAdministrator(platformRole))
            throw new UnauthorizedAccessException("فقط مدیران پلتفرم می‌توانند scope همه (*) صادر کنند.");

        var raw = "rk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var prefix = raw[..12];
        var client = new ApiClient
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            KeyPrefix = prefix,
            KeyHash = Hash(raw),
            OwnerUserId = ownerUserId,
            Active = true,
            ScopesJson = JsonSerializer.Serialize(normalized),
            CreatedAt = DateTime.UtcNow,
        };
        db.ApiClients.Add(client);
        db.ApiClientWorkspaces.Add(new ApiClientWorkspace
        {
            Id = Guid.NewGuid(),
            ApiClientId = client.Id,
            WorkspaceId = workspaceId,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            id = client.Id,
            name = client.Name,
            keyPrefix = client.KeyPrefix,
            apiKey = raw, // shown once
            workspaceId,
            scopes = normalized,
            note = "این کلید فقط یک‌بار نمایش داده می‌شود. آن را در جای امن ذخیره کن.",
        };
    }

    public async Task<object> ListAsync(int ownerUserId, Guid? workspaceId, CancellationToken cancellationToken)
    {
        var query = db.ApiClients.AsNoTracking().Where(item => item.OwnerUserId == ownerUserId && item.RevokedAt == null);
        if (workspaceId is Guid ws)
        {
            var ids = await db.ApiClientWorkspaces.AsNoTracking()
                .Where(item => item.WorkspaceId == ws)
                .Select(item => item.ApiClientId)
                .ToListAsync(cancellationToken);
            query = query.Where(item => ids.Contains(item.Id));
        }

        var items = await query.OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                name = item.Name,
                keyPrefix = item.KeyPrefix,
                active = item.Active,
                lastUsedAt = item.LastUsedAt,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items, suggestedScopes = SuggestedLeastPrivilegeScopes };
    }

    public async Task<object> RevokeAsync(int ownerUserId, Guid clientId, CancellationToken cancellationToken)
    {
        var client = await db.ApiClients.FirstOrDefaultAsync(item => item.Id == clientId && item.OwnerUserId == ownerUserId, cancellationToken)
            ?? throw new InvalidOperationException("کلاینت پیدا نشد.");
        client.Active = false;
        client.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true };
    }

    public async Task<(ApiClient Client, Guid WorkspaceId)?> AuthenticateAsync(string? apiKey, Guid? requestedWorkspaceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith("rk_", StringComparison.Ordinal)) return null;
        var hash = Hash(apiKey.Trim());
        var client = await db.ApiClients.FirstOrDefaultAsync(item => item.KeyHash == hash && item.Active && item.RevokedAt == null, cancellationToken);
        if (client is null) return null;

        var links = await db.ApiClientWorkspaces.AsNoTracking()
            .Where(item => item.ApiClientId == client.Id)
            .Select(item => item.WorkspaceId)
            .ToListAsync(cancellationToken);
        if (links.Count == 0) return null;

        Guid workspaceId;
        if (requestedWorkspaceId is Guid requested)
        {
            if (!links.Contains(requested)) return null;
            workspaceId = requested;
        }
        else
        {
            workspaceId = links[0];
        }

        client.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return (client, workspaceId);
    }

    public static bool HasScope(ApiClient client, string scope)
    {
        try
        {
            var scopes = JsonSerializer.Deserialize<string[]>(client.ScopesJson) ?? [];
            return scopes.Contains("*", StringComparer.OrdinalIgnoreCase)
                || scopes.Contains(scope, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
