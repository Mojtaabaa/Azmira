namespace KonkoorHelp.Api.Services.Gamification;

/// <summary>
/// Local-day helpers for daily missions. Uses workspace timezone (same as streaks).
/// DST: TimeZoneInfo conversion applies OS transition rules.
/// Timezone change: already-issued missions keep their UTC StartsAt/EndsAt window;
/// a TZ change does not rewrite or duplicate that day's instances.
/// </summary>
public static class MissionClock
{
    public const string DefaultTimeZone = "Asia/Tehran";

    public static DateOnly ToLocalDate(DateTime utc, string timeZoneId)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(NormalizeTz(timeZoneId));
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            var offset = timeZoneId is "Asia/Tehran" or "Iran Standard Time"
                ? TimeSpan.FromHours(3.5)
                : TimeSpan.Zero;
            return DateOnly.FromDateTime(utc + offset);
        }
    }

    public static (DateTime StartUtc, DateTime EndUtc) LocalDayUtcBounds(DateOnly localDate, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(NormalizeTz(timeZoneId));
            var startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var endLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
            // End exclusive → store inclusive EndsAt one tick before next midnight UTC
            return (startUtc, endUtc.AddTicks(-1));
        }
        catch (TimeZoneNotFoundException)
        {
            var offset = timeZoneId is "Asia/Tehran" or "Iran Standard Time"
                ? TimeSpan.FromHours(3.5)
                : TimeSpan.Zero;
            var startUtc = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - offset;
            var endUtc = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - offset;
            return (startUtc, endUtc.AddTicks(-1));
        }
    }

    public static string NormalizeTz(string timeZoneId) =>
        timeZoneId switch
        {
            "Asia/Tehran" => OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran",
            "Iran Standard Time" => OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran",
            _ => string.IsNullOrWhiteSpace(timeZoneId) ? NormalizeTz(DefaultTimeZone) : timeZoneId,
        };
}
