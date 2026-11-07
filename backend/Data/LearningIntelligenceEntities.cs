namespace KonkoorHelp.Api.Data;

public enum QuestionType : byte
{
    MultipleChoice = 1,
    TrueFalse = 2,
    Descriptive = 3,
}

public enum AssessmentKind : byte
{
    TopicQuiz = 1,
    ChapterAssessment = 2,
    WeeklyAssessment = 3,
    TeacherExam = 4,
    SelfAssessment = 5,
}

public enum AssessmentStatus : byte
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public enum AttemptStatus : byte
{
    InProgress = 1,
    Submitted = 2,
    Scored = 3,
}

public enum MasteryLevel : byte
{
    NotStarted = 0,
    Learning = 1,
    NeedsReview = 2,
    Developing = 3,
    Proficient = 4,
    Mastered = 5,
}

public enum ReviewOutcome : byte
{
    Fail = 0,
    Hard = 1,
    Good = 2,
    Easy = 3,
}

public enum AiRecommendationKind : byte
{
    StudyPlan = 1,
    Review = 2,
    Quest = 3,
    Schedule = 4,
    Habit = 5,
    Weakness = 6,
}

public enum AiRecommendationStatus : byte
{
    Suggested = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4,
}

public enum AiReportKind : byte
{
    Weekly = 1,
    ProgressSummary = 2,
}

public sealed class AssessmentDefinition
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public AssessmentKind Kind { get; set; } = AssessmentKind.SelfAssessment;
    public AssessmentStatus Status { get; set; } = AssessmentStatus.Draft;
    public string Subject { get; set; } = "";
    public string? Chapter { get; set; }
    public string? Topic { get; set; }
    public Guid? ClassId { get; set; }
    public int CreatedByUserId { get; set; }
    public int? TimeLimitMinutes { get; set; }
    public double PassScore { get; set; } = 70;
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<AssessmentQuestion> Questions { get; set; } = [];
}

public sealed class AssessmentQuestion
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public int Ordinal { get; set; }
    public QuestionType Type { get; set; }
    public string Prompt { get; set; } = "";
    public string OptionsJson { get; set; } = "[]";
    public string CorrectAnswerJson { get; set; } = "null";
    public double Points { get; set; } = 1;
    public string Subject { get; set; } = "";
    public string? Chapter { get; set; }
    public string? Topic { get; set; }
    public AssessmentDefinition Assessment { get; set; } = null!;
}

public sealed class AssessmentAttempt
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AssessmentId { get; set; }
    public int UserId { get; set; }
    public AttemptStatus Status { get; set; } = AttemptStatus.InProgress;
    public DateTime StartedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int DurationSeconds { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public double Percent { get; set; }
    public AssessmentDefinition Assessment { get; set; } = null!;
    public List<AssessmentAnswer> Answers { get; set; } = [];
}

public sealed class AssessmentAnswer
{
    public Guid Id { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string ResponseJson { get; set; } = "null";
    public bool? IsCorrect { get; set; }
    public double PointsAwarded { get; set; }
    public int? DurationSeconds { get; set; }
    public AssessmentAttempt Attempt { get; set; } = null!;
    public AssessmentQuestion Question { get; set; } = null!;
}

public sealed class MasteryNode
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string Subject { get; set; } = "";
    public string? Chapter { get; set; }
    public string? Topic { get; set; }
    public MasteryLevel Level { get; set; } = MasteryLevel.NotStarted;
    public double ScoreEma { get; set; }
    public int EvidenceCount { get; set; }
    public int CorrectCount { get; set; }
    public int ReviewSuccessCount { get; set; }
    public int ReviewFailCount { get; set; }
    public DateTime? LastEvidenceAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SpacedRepetitionCard
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string Subject { get; set; } = "";
    public string? Chapter { get; set; }
    public string Topic { get; set; } = "";
    public string AlgorithmCode { get; set; } = "sm2-lite";
    public string AlgorithmStateJson { get; set; } = "{}";
    public byte Difficulty { get; set; } = 3;
    public byte Confidence { get; set; } = 3;
    public int IntervalDays { get; set; } = 1;
    public double EaseFactor { get; set; } = 2.5;
    public int Repetition { get; set; }
    public DateOnly NextReviewDate { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SpacedRepetitionReview
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public ReviewOutcome Outcome { get; set; }
    public byte DifficultyRating { get; set; }
    public byte ConfidenceRating { get; set; }
    public bool Successful { get; set; }
    public int IntervalBeforeDays { get; set; }
    public int IntervalAfterDays { get; set; }
    public string AlgorithmCode { get; set; } = "sm2-lite";
    public string StateBeforeJson { get; set; } = "{}";
    public string StateAfterJson { get; set; } = "{}";
    public DateTime ReviewedAt { get; set; }
    public SpacedRepetitionCard Card { get; set; } = null!;
}

public sealed class SpacedRepetitionProfile
{
    public Guid Id { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Code { get; set; } = "default";
    public string Name { get; set; } = "";
    public string AlgorithmCode { get; set; } = "sm2-lite";
    public string ConfigJson { get; set; } = "{}";
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiWorkspaceSettings
{
    public Guid WorkspaceId { get; set; }
    public bool Enabled { get; set; }
    public string ProviderCode { get; set; } = "null";
    public string Model { get; set; } = "";
    public bool AllowUnderAge { get; set; }
    public int MinAgeYears { get; set; } = 13;
    public string SystemPromptExtra { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public sealed class AiRecommendation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public AiRecommendationKind Kind { get; set; }
    public AiRecommendationStatus Status { get; set; } = AiRecommendationStatus.Suggested;
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string FactsJson { get; set; } = "{}";
    public string ProviderCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByUserId { get; set; }
}

public sealed class AiWeeklyReport
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public AiReportKind Kind { get; set; } = AiReportKind.Weekly;
    public string PeriodKey { get; set; } = "";
    public string FactsJson { get; set; } = "{}";
    public string Narrative { get; set; } = "";
    public string RecommendationsJson { get; set; } = "[]";
    public string ProviderCode { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}

public sealed class AiPlanSuggestion
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int UserId { get; set; }
    public AiRecommendationStatus Status { get; set; } = AiRecommendationStatus.Suggested;
    public string Title { get; set; } = "";
    public string ScheduleJson { get; set; } = "[]";
    public string FactsJson { get; set; } = "{}";
    public string ProviderCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
