namespace KonkoorHelp.Api.Data;

/// <summary>Curriculum pack or workspace-scoped journey definition.</summary>
public sealed class JourneyDefinition
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string Locale { get; set; } = "fa-IR";
    public int Version { get; set; } = 1;
    public bool IsSystem { get; set; }
    public bool IsBootstrap { get; set; }
    /// <summary>School-authored packs become assignable when published.</summary>
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<JourneyRegion> Regions { get; set; } = [];
}

public sealed class JourneyRegion
{
    public Guid Id { get; set; }
    public Guid JourneyId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public string VisualThemeKey { get; set; } = "default";
    public string SubjectKey { get; set; } = "";
    public JourneyDefinition Journey { get; set; } = null!;
    public List<JourneyStage> Stages { get; set; } = [];
}

public sealed class JourneyStage
{
    public Guid Id { get; set; }
    public Guid RegionId { get; set; }
    public int SortOrder { get; set; }
    public string Title { get; set; } = "";
    public string? NarrativeBlurb { get; set; }
    /// <summary>JSON: evidence thresholds (mastery/assessment/SRS). Never XP/time alone.</summary>
    public string RequirementsJson { get; set; } = "{}";
    public Guid? UnlockPriorStageId { get; set; }
    /// <summary>JSON: optional coins / badgeCode / cosmeticKey hooks.</summary>
    public string RewardsJson { get; set; } = "{}";
    public JourneyRegion Region { get; set; } = null!;
}

public sealed class LearnerJourneyProgress
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public Guid JourneyId { get; set; }
    public Guid? CurrentStageId { get; set; }
    public string UnlockedStageIdsJson { get; set; } = "[]";
    public string CompletedStageIdsJson { get; set; } = "[]";
    /// <summary>JSON array of evidence refs explaining unlocks.</summary>
    public string EvidenceJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
    public JourneyDefinition Journey { get; set; } = null!;
}
