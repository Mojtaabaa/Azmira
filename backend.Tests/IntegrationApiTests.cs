using System.Security.Cryptography;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class IntegrationApiTests
{
    private static ServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddScoped<ApiClientService>();
        services.AddScoped<WebhookService>();
        services.AddHttpClient(nameof(WebhookService));
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Webhook_signature_is_stable_hmac()
    {
        var body = """{"type":"LevelUp","data":{"level":2}}""";
        var secret = "abc123";
        var sig = WebhookService.Sign(secret, body);
        Assert.StartsWith("sha256=", sig);
        Assert.True(WebhookService.VerifySignature(secret, body, sig));
        Assert.False(WebhookService.VerifySignature(secret, body, sig + "x"));
    }

    [Fact]
    public void Api_client_hash_is_one_way()
    {
        var raw = "rk_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var hash = ApiClientService.Hash(raw);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(raw, hash);
        Assert.Equal(hash, ApiClientService.Hash(raw));
    }

    [Fact]
    public async Task Api_key_auth_enforces_workspace_binding_and_scopes()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clients = scope.ServiceProvider.GetRequiredService<ApiClientService>();

        var owner = new UserAccount
        {
            Id = 9101,
            Username = "apiowner",
            NormalizedUsername = "apiowner",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        var outsider = new UserAccount
        {
            Id = 9102,
            Username = "outsider",
            NormalizedUsername = "outsider",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.AddRange(owner, outsider);
        var wsA = Guid.NewGuid();
        var wsB = Guid.NewGuid();
        db.Workspaces.AddRange(
            new Workspace { Id = wsA, Type = WorkspaceType.Personal, Name = "A", OwnerUserId = owner.Id, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Workspace { Id = wsB, Type = WorkspaceType.Personal, Name = "B", OwnerUserId = outsider.Id, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = wsA,
            UserId = owner.Id,
            Role = WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(
            await clients.CreateAsync(owner.Id, "test", wsA, ["gamification:read", "quests:read"], CancellationToken.None)));
        var apiKey = created.RootElement.GetProperty("apiKey").GetString()!;

        var ok = await clients.AuthenticateAsync(apiKey, wsA, CancellationToken.None);
        Assert.NotNull(ok);
        Assert.Equal(wsA, ok!.Value.WorkspaceId);
        Assert.True(ApiClientService.HasScope(ok.Value.Client, "gamification:read"));
        Assert.False(ApiClientService.HasScope(ok.Value.Client, "rewards:read"));

        var crossTenant = await clients.AuthenticateAsync(apiKey, wsB, CancellationToken.None);
        Assert.Null(crossTenant);

        Assert.Null(await clients.AuthenticateAsync("rk_deadbeef", wsA, CancellationToken.None));
    }

    [Fact]
    public async Task Webhook_enqueue_is_tenant_scoped_with_signed_delivery_log()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();

        var user = new UserAccount
        {
            Id = 9201,
            Username = "hookuser",
            NormalizedUsername = "hookuser",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        var ws = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Workspaces.AddRange(
            new Workspace { Id = ws, Type = WorkspaceType.Personal, Name = "hook", OwnerUserId = user.Id, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Workspace { Id = other, Type = WorkspaceType.Personal, Name = "other", OwnerUserId = user.Id, Status = WorkspaceStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await webhooks.CreateEndpointAsync(ws, user.Id, "http://127.0.0.1:9/hooks", ["LevelUp", "StudySessionCompleted"], CancellationToken.None);
        await webhooks.EnqueueForEventAsync(ws, Guid.NewGuid(), WebhookEventTypes.LevelUp, new { level = 3 }, CancellationToken.None);
        await webhooks.EnqueueForEventAsync(other, Guid.NewGuid(), WebhookEventTypes.LevelUp, new { level = 9 }, CancellationToken.None);

        Assert.Equal(1, await db.WebhookDeliveries.CountAsync(item => item.WorkspaceId == ws));
        Assert.Equal(0, await db.WebhookDeliveries.CountAsync(item => item.WorkspaceId == other));

        var delivery = await db.WebhookDeliveries.Include(item => item.Endpoint).SingleAsync();
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.True(WebhookService.VerifySignature(delivery.Endpoint.Secret, delivery.PayloadJson, delivery.Signature));

        await webhooks.ProcessPendingAsync(CancellationToken.None, take: 5);
        delivery = await db.WebhookDeliveries.SingleAsync();
        Assert.True(delivery.Attempts >= 1);
        Assert.True(delivery.Status is WebhookDeliveryStatus.Pending or WebhookDeliveryStatus.DeadLetter or WebhookDeliveryStatus.Failed);
    }

    [Fact]
    public async Task Assessment_completed_maps_from_submitted_event()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();
        var user = new UserAccount
        {
            Id = 9301,
            Username = "assess",
            NormalizedUsername = "assess",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = WorkspaceType.Personal,
            Name = "p",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await webhooks.CreateEndpointAsync(ws, user.Id, "https://example.com/hook", ["AssessmentCompleted"], CancellationToken.None);
        await webhooks.EnqueueForEventAsync(ws, Guid.NewGuid(), DomainEventTypes.AssessmentSubmitted, new { score = 90 }, CancellationToken.None);
        var delivery = await db.WebhookDeliveries.SingleAsync();
        Assert.Equal(WebhookEventTypes.AssessmentCompleted, delivery.EventType);
    }
}
