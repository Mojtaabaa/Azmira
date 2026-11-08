namespace KonkoorHelp.Api.Data;

public enum ClassMemberRole : byte
{
    Student = 10,
    Teacher = 30,
}

public enum TeamMemberRole : byte
{
    Member = 1,
    Captain = 2,
}

public enum TeamInviteStatus : byte
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
}

public enum CompetitionStatus : byte
{
    Draft = 0,
    Active = 1,
    Ended = 2,
    Cancelled = 3,
}

public enum CompetitionType : byte
{
    Individual = 1,
    Team = 2,
}

public enum StoreItemCategory : byte
{
    AvatarBase = 1,
    Accessory = 2,
    Clothing = 3,
    Background = 4,
    Frame = 5,
    DigitalReward = 6,
    SchoolReward = 7,
}

public enum RewardGrantKind : byte
{
    Digital = 1,
    School = 2,
    Teacher = 3,
    Certificate = 4,
    Physical = 5,
}

public enum RewardGrantStatus : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Fulfilled = 4,
}

public enum LeaderboardMetric : byte
{
    WeeklyXp = 1,
    MonthlyXp = 2,
    Consistency = 3,
    GoalCompletion = 4,
    LearningImprovement = 5,
    SubjectPerformance = 6,
    QuestCompletion = 7,
    Team = 8,
    Class = 9,
    School = 10,
}

public sealed class UserPrivacySettings
{
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    /// <summary>Default private — school cohort is opt-in (Phase 12).</summary>
    public bool LeaderboardVisible { get; set; }
    public bool ShowRealName { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}

public sealed class LeaderboardDefinition
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public LeaderboardMetric Metric { get; set; }
    public string PeriodCode { get; set; } = "week";
    public bool Enabled { get; set; } = true;
    public string Visibility { get; set; } = "members";
    public DateTime CreatedAt { get; set; }
}

public sealed class LeaderboardSnapshot
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public LeaderboardMetric Metric { get; set; }
    public string PeriodKey { get; set; } = "";
    public string? Subject { get; set; }
    public Guid? ClassId { get; set; }
    public string PayloadJson { get; set; } = "[]";
    public DateTime GeneratedAt { get; set; }
}

public sealed class ClassGroup
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public Guid? GradeId { get; set; }
    public string Name { get; set; } = "";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<ClassMember> Members { get; set; } = [];
}

public sealed class ClassMember
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public int UserId { get; set; }
    public ClassMemberRole Role { get; set; } = ClassMemberRole.Student;
    public DateTime JoinedAt { get; set; }
    public ClassGroup Class { get; set; } = null!;
}

public sealed class Team
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ClassId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<TeamMember> Members { get; set; } = [];
}

public sealed class TeamMember
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public int UserId { get; set; }
    public TeamMemberRole Role { get; set; } = TeamMemberRole.Member;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public Team Team { get; set; } = null!;
}

public sealed class TeamInvite
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public int InvitedUserId { get; set; }
    public int InvitedByUserId { get; set; }
    public TeamInviteStatus Status { get; set; } = TeamInviteStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public Team Team { get; set; } = null!;
}

public sealed class TeamChallenge
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int TargetXp { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Team Team { get; set; } = null!;
}

public sealed class TeamAchievement
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime AwardedAt { get; set; }
}

public sealed class Competition
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ClassId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public CompetitionType Type { get; set; } = CompetitionType.Individual;
    public CompetitionStatus Status { get; set; } = CompetitionStatus.Draft;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string ScoringRulesJson { get; set; } = "{}";
    public string RewardsJson { get; set; } = "{}";
    public string Visibility { get; set; } = "members";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CompetitionParticipant
{
    public Guid Id { get; set; }
    public Guid CompetitionId { get; set; }
    public int UserId { get; set; }
    public int Score { get; set; }
    public DateTime JoinedAt { get; set; }
    public Competition Competition { get; set; } = null!;
}

public sealed class CompetitionTeam
{
    public Guid Id { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid TeamId { get; set; }
    public int Score { get; set; }
    public DateTime JoinedAt { get; set; }
    public Competition Competition { get; set; } = null!;
}

public sealed class StoreItem
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public StoreItemCategory Category { get; set; }
    public int PriceCoins { get; set; }
    public string AssetKey { get; set; } = "";
    public string CssClass { get; set; } = "";
    public bool Stackable { get; set; }
    public bool IsSystem { get; set; }
    public bool Active { get; set; } = true;
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserInventoryItem
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public bool Equipped { get; set; }
    public DateTime AcquiredAt { get; set; }
    public StoreItem Item { get; set; } = null!;
}

public sealed class StorePurchase
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid ItemId { get; set; }
    public int PriceCoins { get; set; }
    public Guid? CoinLedgerId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserAvatarLoadout
{
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid? BaseItemId { get; set; }
    public Guid? AccessoryItemId { get; set; }
    public Guid? ClothingItemId { get; set; }
    public Guid? BackgroundItemId { get; set; }
    public Guid? FrameItemId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class RewardGrant
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public RewardGrantKind Kind { get; set; }
    public RewardGrantStatus Status { get; set; } = RewardGrantStatus.Pending;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int? CoinAmount { get; set; }
    public int? XpAmount { get; set; }
    public Guid? StoreItemId { get; set; }
    public Guid? CompetitionId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
