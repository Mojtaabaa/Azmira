using System.Text.Json.Serialization;

namespace KonkoorHelp.Api.Services.Learning;

public sealed class JourneyStageRequirements
{
    /// <summary>Minimum MasteryLevel enum value (e.g. 3 = Developing).</summary>
    public int? MinMasteryLevel { get; set; }
    public double? MinScoreEma { get; set; }
    public string? Subject { get; set; }
    public string? Topic { get; set; }
    public double? MinAssessmentPercent { get; set; }
    public int? MinSrsCorrectReviews { get; set; }
    public bool AlwaysOpen { get; set; }
}

public sealed class JourneyStageRewards
{
    public int? Coins { get; set; }
    public string? BadgeCode { get; set; }
    /// <summary>Store/cosmetic unlock key (achievement-linked; not purchasable mastery cosmetics).</summary>
    public string? CosmeticKey { get; set; }
}

public sealed class JourneyEvidenceRef
{
    public Guid StageId { get; set; }
    public string Type { get; set; } = "";
    public string RefId { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime At { get; set; }
}

public static class JourneyJson
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
