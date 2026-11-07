namespace KonkoorHelp.Api.Data;

public enum WebhookDeliveryStatus : byte
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    DeadLetter = 4,
}

public sealed class ApiClient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public int OwnerUserId { get; set; }
    public bool Active { get; set; } = true;
    public string ScopesJson { get; set; } = "[]";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class ApiClientWorkspace
{
    public Guid Id { get; set; }
    public Guid ApiClientId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApiClient ApiClient { get; set; } = null!;
}

public sealed class WebhookEndpoint
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Url { get; set; } = "";
    public string Secret { get; set; } = "";
    public string EventTypesJson { get; set; } = "[]";
    public bool Active { get; set; } = true;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid WebhookEndpointId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? EventId { get; set; }
    public string EventType { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string Signature { get; set; } = "";
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int Attempts { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public WebhookEndpoint Endpoint { get; set; } = null!;
}
