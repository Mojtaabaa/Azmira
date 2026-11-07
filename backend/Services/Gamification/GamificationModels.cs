using System.Text.Json;
using System.Text.Json.Serialization;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class ProfileRulesDocument
{
    public int Version { get; set; } = 1;
    public List<XpAwardRule> Awards { get; set; } = [];
    public List<string> DisabledEvents { get; set; } = [];
}

public sealed class XpAwardRule
{
    public string Code { get; set; } = "";
    public string On { get; set; } = "";
    public int Xp { get; set; }
    public int? Coins { get; set; }
    public int? PerMinutes { get; set; }
    public double? MinScore { get; set; }
    public int? MaxPerDay { get; set; }
}

public sealed class LevelDefinition
{
    public int Level { get; set; }
    public string Title { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public int RequiredTotalXp { get; set; }
    public string Icon { get; set; } = "level";
}

public sealed class StreakSettings
{
    public string DefaultTimeZone { get; set; } = "Asia/Tehran";
    public int MinMinutesPerDay { get; set; } = 15;
    public int WeeklyRestAllowance { get; set; } = 1;
    public int StartingFreezes { get; set; } = 2;
    public List<int> MilestoneDays { get; set; } = [7, 30, 100];
    public int MilestoneXp { get; set; } = 30;
}

public static class DomainEventTypes
{
    public const string StudySessionCompleted = "StudySessionCompleted";
    public const string StudyGoalCompleted = "StudyGoalCompleted";
    public const string WeeklyGoalCompleted = "WeeklyGoalCompleted";
    public const string AssignmentCompleted = "AssignmentCompleted";
    public const string ReviewCompleted = "ReviewCompleted";
    public const string AssessmentSubmitted = "AssessmentSubmitted";
    public const string AssessmentPassed = "AssessmentPassed";
    public const string MasteryImproved = "MasteryImproved";
    public const string QuestCompleted = "QuestCompleted";
    public const string XpReversed = "XpReversed";
}

public sealed class DomainEventEnvelope
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = "";
    public int EventVersion { get; set; } = 1;
    public DateTime OccurredAt { get; set; }
    public Guid WorkspaceId { get; set; }
    public int? ActorUserId { get; set; }
    public string IdempotencyKey { get; set; } = "";
    public string? CorrelationId { get; set; }
    public JsonElement Data { get; set; }
}

public static class GamificationJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
