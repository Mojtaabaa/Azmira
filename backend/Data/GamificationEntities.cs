namespace KonkoorHelp.Api.Data;

public enum OutboxStatus : byte
{
    Pending = 1,
    Processed = 2,
    Failed = 3,
}

public enum LedgerKind : byte
{
    Earn = 1,
    Reverse = 2,
    Adjust = 3,
}

public enum BadgeTier : byte
{
    Bronze = 1,
    Silver = 2,
    Gold = 3,
    Diamond = 4,
}

public enum QuestKind : byte
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Special = 4,
    TeacherAssigned = 5,
    SchoolWide = 6,
    Personalized = 7,
}

public enum QuestStatus : byte
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Expired = 3,
    Claimed = 4,
    Cancelled = 5,
}

public enum QuestVisibility : byte
{
    Public = 1,
    Members = 2,
    Hidden = 3,
}

public enum StreakDayKind : byte
{
    Study = 1,
    Rest = 2,
    Freeze = 3,
}

public enum GameNotificationKind : byte
{
    XpGained = 1,
    LevelUp = 2,
    BadgeUnlocked = 3,
    QuestCompleted = 4,
    StreakMilestone = 5,
}

public sealed class GamificationProfile
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsSystem { get; set; }
    public string RulesJson { get; set; } = "{}";
    public string LevelsJson { get; set; } = "[]";
    public string StreakJson { get; set; } = "{}";
    public int DailyXpCap { get; set; } = 200;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class DomainOutbox
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int? UserId { get; set; }
    public string EventType { get; set; } = "";
    public int EventVersion { get; set; } = 1;
    public DateTime OccurredAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ProcessedEvent
{
    public string ConsumerName { get; set; } = "";
    public Guid EventId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public sealed class XpLedger
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid? EventId { get; set; }
    public int Delta { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = "";
    public string? RuleCode { get; set; }
    public LedgerKind Kind { get; set; } = LedgerKind.Earn;
    public DateTime CreatedAt { get; set; }
}

public sealed class CoinLedger
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid? EventId { get; set; }
    public int Delta { get; set; }
    public int BalanceAfter { get; set; }
    public string Reason { get; set; } = "";
    public string? RuleCode { get; set; }
    public LedgerKind Kind { get; set; } = LedgerKind.Earn;
    public DateTime CreatedAt { get; set; }
}

public sealed class UserGameStats
{
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public int XpTotal { get; set; }
    public int Level { get; set; } = 1;
    public int Coins { get; set; }
    public int StreakCurrent { get; set; }
    public int StreakBest { get; set; }
    public int StreakFreezes { get; set; }
    public DateOnly? LastStudyLocalDate { get; set; }
    public string TimeZone { get; set; } = "Asia/Tehran";
    public DateTime UpdatedAt { get; set; }
}

public sealed class BadgeDefinition
{
    public Guid Id { get; set; }
    public Guid? ProfileId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public BadgeTier Tier { get; set; } = BadgeTier.Bronze;
    public string Icon { get; set; } = "badge";
    public bool IsHidden { get; set; }
    public bool AllowRepeat { get; set; }
    public string ConditionsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}

public sealed class UserBadge
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid BadgeId { get; set; }
    public Guid? EventId { get; set; }
    public DateTime AwardedAt { get; set; }
    public BadgeDefinition Badge { get; set; } = null!;
}

public sealed class QuestDefinition
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "general";
    public QuestKind Kind { get; set; } = QuestKind.Daily;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int TargetValue { get; set; } = 1;
    public int RewardXp { get; set; }
    public int RewardCoins { get; set; }
    public string CompletionCondition { get; set; } = "";
    public QuestVisibility Visibility { get; set; } = QuestVisibility.Public;
    public byte Difficulty { get; set; } = 1;
    public QuestStatus Status { get; set; } = QuestStatus.Active;
    public int? AssignedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UserQuestProgress
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid QuestId { get; set; }
    public int Progress { get; set; }
    public QuestStatus Status { get; set; } = QuestStatus.Active;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public QuestDefinition Quest { get; set; } = null!;
}

public sealed class StreakDay
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public StreakDayKind Kind { get; set; } = StreakDayKind.Study;
    public int Minutes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GameNotification
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public GameNotificationKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public int ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public string? TargetId { get; set; }
    public string DetailJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
