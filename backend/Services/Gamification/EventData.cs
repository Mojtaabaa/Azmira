using System.Text.Json;

namespace KonkoorHelp.Api.Services.Gamification;

internal static class EventData
{
    public static int ReadInt(JsonElement data, string key)
    {
        if (data.ValueKind != JsonValueKind.Object) return 0;
        if (!data.TryGetProperty(key, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n)) return n;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }

    public static double ReadDouble(JsonElement data, string key)
    {
        if (data.ValueKind != JsonValueKind.Object) return 0;
        if (!data.TryGetProperty(key, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n)) return n;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0;
    }
}
