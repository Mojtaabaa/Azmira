namespace KonkoorHelp.Api.Data;

public enum GuardianLinkStatus : byte
{
    Pending = 1,
    Verified = 2,
    Revoked = 3,
}

public enum AssignmentStatus : byte
{
    Draft = 0,
    Published = 1,
    Closed = 2,
}

public enum AssignmentSubmissionStatus : byte
{
    NotStarted = 0,
    Submitted = 1,
    Returned = 2,
}

public sealed class School
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public string Description { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public int OwnerUserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public sealed class AcademicYear
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartsOn { get; set; }
    public DateOnly EndsOn { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class GradeLevel
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SchoolSubject
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool Active { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public sealed class GuardianStudentLink
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int ParentUserId { get; set; }
    public int StudentUserId { get; set; }
    public GuardianLinkStatus Status { get; set; } = GuardianLinkStatus.Pending;
    public int? VerifiedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public sealed class SchoolAssignment
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ClassId { get; set; }
    public Guid? SubjectId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Draft;
    public DateTime? DueAt { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class SchoolAssignmentSubmission
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int StudentUserId { get; set; }
    public AssignmentSubmissionStatus Status { get; set; } = AssignmentSubmissionStatus.NotStarted;
    public string Content { get; set; } = "";
    public double? Score { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public SchoolAssignment Assignment { get; set; } = null!;
}

public sealed class TeacherFeedback
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? ClassId { get; set; }
    public int TeacherUserId { get; set; }
    public int StudentUserId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class SchoolSettingsExtras
{
    public Guid WorkspaceId { get; set; }
    public bool LeaderboardsVisible { get; set; } = true;
    public bool AllowTeacherBadges { get; set; } = true;
    public bool AllowParentReadOnly { get; set; } = true;
    public string AnalyticsDefaultsJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

public enum SchoolRegistrationRequestStatus : byte
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public sealed class SchoolRegistrationRequest
{
    public Guid Id { get; set; }
    public int RequesterUserId { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Description { get; set; } = "";
    public SchoolRegistrationRequestStatus Status { get; set; } = SchoolRegistrationRequestStatus.Pending;
    public int? ReviewerUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Notes { get; set; } = "";
    public Guid? CreatedSchoolId { get; set; }
    public Guid? CreatedWorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
