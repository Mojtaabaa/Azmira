namespace KonkoorHelp.Api.Data;

public enum WorkspaceType : byte
{
    Personal = 1,
    School = 2,
}

public enum WorkspaceMemberRole : byte
{
    Student = 10,
    Parent = 20,
    Teacher = 30,
    AcademicManager = 40,
    SchoolAdministrator = 50,
    SchoolOwner = 60,
}

public enum WorkspaceMemberStatus : byte
{
    Invited = 1,
    Active = 2,
    Suspended = 3,
    Left = 4,
}

public enum WorkspaceStatus : byte
{
    Active = 1,
    Suspended = 2,
    Archived = 3,
}

public enum SubscriptionStatus : byte
{
    Trial = 1,
    Active = 2,
    PastDue = 3,
    Canceled = 4,
}

public sealed class Workspace
{
    public Guid Id { get; set; }
    public WorkspaceType Type { get; set; }
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public Guid? SchoolId { get; set; }
    public int OwnerUserId { get; set; }
    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Active;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public UserAccount Owner { get; set; } = null!;
    public WorkspaceSettings? Settings { get; set; }
    public List<WorkspaceMember> Members { get; set; } = [];
}

public sealed class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public WorkspaceMemberRole Role { get; set; }
    public WorkspaceMemberStatus Status { get; set; } = WorkspaceMemberStatus.Active;
    public int? InvitedByUserId { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public sealed class WorkspaceSettings
{
    public Guid WorkspaceId { get; set; }
    public string Data { get; set; } = "{}";
    public string LocaleDefault { get; set; } = "fa-IR";
    public string TimeZone { get; set; } = "Asia/Tehran";
    public string FeatureFlags { get; set; } = "{}";
    public Guid? GamificationProfileId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Workspace Workspace { get; set; } = null!;
}

public sealed class WorkspaceSubscription
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string PlanCode { get; set; } = "free";
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public int? SeatLimit { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Workspace Workspace { get; set; } = null!;
}

public sealed record WorkspaceContext(
    Guid WorkspaceId,
    WorkspaceType Type,
    WorkspaceMemberRole Role,
    int UserId,
    string WorkspaceName);
