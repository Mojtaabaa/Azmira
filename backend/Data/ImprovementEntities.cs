namespace KonkoorHelp.Api.Data;

/// <summary>Self-baseline for personal improvement (not cohort rank).</summary>
public sealed class PersonalBaseline
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    /// <summary>Empty string = cross-subject composite.</summary>
    public string SubjectKey { get; set; } = "";
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    /// <summary>0–1 mean of mastery EMA / assessment percent in the baseline window.</summary>
    public double BaselineScore { get; set; }
    public int EvidenceEventCount { get; set; }
    public string EvidenceSummaryJson { get; set; } = "[]";
    public DateTime EstablishedAt { get; set; }
    public DateTime? RebaselinedAt { get; set; }
}

public sealed class ImprovementSnapshot
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string SubjectKey { get; set; } = "";
    public string PeriodKey { get; set; } = "";
    public double AbsoluteScore { get; set; }
    public double ImprovementPercent { get; set; }
    public int EvidenceEventCount { get; set; }
    public DateTime CapturedAt { get; set; }
}

public sealed class CoachThread
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<CoachMessage> Messages { get; set; } = [];
}

public sealed class CoachMessage
{
    public Guid Id { get; set; }
    public Guid ThreadId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    /// <summary>high | medium | low | empty for user turns.</summary>
    public string Confidence { get; set; } = "";
    public string EvidenceRefsJson { get; set; } = "[]";
    public string ProviderCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public CoachThread Thread { get; set; } = null!;
}
