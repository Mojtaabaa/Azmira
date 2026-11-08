using System.Security.Cryptography;
using System.Text;

namespace KonkoorHelp.Api.Services.Gamification;

/// <summary>
/// Deterministic IDs so concurrent assignment of the same day/template
/// collapses to one QuestDefinition row (PK collision) without a new table.
/// </summary>
public static class DailyMissionIds
{
    public static Guid QuestId(Guid workspaceId, int userId, DateOnly localDate, string templateId) =>
        FromUtf8($"dm:q:{workspaceId:N}:{userId}:{localDate:yyyy-MM-dd}:{templateId}");

    public static Guid ProgressId(Guid workspaceId, int userId, DateOnly localDate, string templateId) =>
        FromUtf8($"dm:p:{workspaceId:N}:{userId}:{localDate:yyyy-MM-dd}:{templateId}");

    private static Guid FromUtf8(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        // RFC 4122 variant bits
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50); // version 5-ish
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}
