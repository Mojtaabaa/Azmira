namespace KonkoorHelp.Api.Data;

public sealed class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string NormalizedUsername { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string? DisplayName { get; set; }
    public string? Locale { get; set; }
    public byte PlatformRole { get; set; }
    public Guid? LastWorkspaceId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public UserSettings? Settings { get; set; }
}

public sealed class AuthSession
{
    public string TokenHash { get; set; } = "";
    public int UserId { get; set; }
    public long ExpiresAt { get; set; }
    public UserAccount User { get; set; } = null!;
}

public sealed class UserSettings
{
    public int UserId { get; set; }
    public string Data { get; set; } = "{}";
    public UserAccount User { get; set; } = null!;
}

public sealed class StudyRecord
{
    public int Seq { get; set; }
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Collection { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string Data { get; set; } = "{}";
    public UserAccount User { get; set; } = null!;
}

public sealed class NoteDocument
{
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string NoteId { get; set; } = "";
    public string Data { get; set; } = "{}";
    public UserAccount User { get; set; } = null!;
}

public sealed class FolderDocument
{
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string FolderId { get; set; } = "";
    public string Data { get; set; } = "{}";
    public UserAccount User { get; set; } = null!;
}

public sealed class TrashDocument
{
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string TrashId { get; set; } = "";
    public long DeletedAt { get; set; }
    public string Data { get; set; } = "{}";
    public UserAccount User { get; set; } = null!;
}

public sealed class NoteImage
{
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string ImageId { get; set; } = "";
    public string DataUrl { get; set; } = "";
    public long CreatedAt { get; set; }
    public UserAccount User { get; set; } = null!;
}

public sealed class NoteSetting
{
    public int UserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "null";
    public UserAccount User { get; set; } = null!;
}
