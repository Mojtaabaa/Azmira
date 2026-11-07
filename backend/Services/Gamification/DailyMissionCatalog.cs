namespace KonkoorHelp.Api.Services.Gamification;

public sealed record MissionTemplate(
    string TemplateId,
    string Title,
    string Description,
    string CompletionCondition,
    int TargetValue,
    int RewardXp,
    int RewardCoins,
    byte Difficulty,
    string Why,
    string Pillar,
    int EstimatedMinutes);

public static class DailyMissionCatalog
{
    public const string Category = "dm";
    public const int MaxMissionsPerDay = 3;
    public const int MaxDailyMissionXp = 60;
    public const int MaxDailyMissionCoins = 20;

    public static readonly IReadOnlyList<MissionTemplate> Templates =
    [
        new("review_due", "بازبینی سررسید", "حداقل {target} کارت بازبینی سررسید را با نتیجه ثبت کن.", "review_count", 3, 15, 5, 2,
            "بازبینی فاصله‌دار شواهد یادگیری می‌سازد، نه فقط زمان سپری‌شده.", "LEARN", 20),
        new("assessment_try", "یک ارزیابی معنادار", "یک ارزیابی را ارسال کن.", "assessment_count", 1, 15, 5, 2,
            "ارزیابی شواهد تسلط ایجاد می‌کند.", "LEARN", 25),
        new("session_focus", "جلسه تمرکز", "حداقل {target} جلسه مطالعه معتبر ثبت کن.", "session_count", 1, 10, 3, 1,
            "یک جلسه متمرکز از برنامه امروز پشتیبانی می‌کند.", "PLAN", 25),
        new("study_block", "بلوک مطالعه", "حداقل {target} دقیقه مطالعه مؤثر ثبت کن.", "study_minutes", 30, 12, 4, 1,
            "زمان مطالعه به‌تنهایی تسلط نیست؛ این مأموریت فقط فعالیت برنامه‌ریزی‌شده را پاداش می‌دهد.", "PLAN", 30),
        new("daily_goal", "رسیدن به هدف روز", "هدف مطالعه روزانه را کامل کن.", "daily_goal", 1, 18, 6, 2,
            "هدف روز از تنظیمات خودت گرفته می‌شود.", "PLAN", 0),
        new("mastery_push", "بهبود شواهد تسلط", "یک بهبود تسلط ثبت‌شده داشته باش.", "mastery_improved", 1, 20, 6, 3,
            "فقط وقتی شواهد تسلط واقعاً بهتر شود پیشرفت می‌کند.", "IMPROVE", 30),
        new("assessment_pass", "ارزیابی موفق", "یک ارزیابی را با نمره قبولی تمام کن.", "assessment_passed", 1, 20, 8, 3,
            "پاس شدن ارزیابی شواهد قوی‌تری از تسلط است.", "LEARN", 30),
    ];

    public static string MetaPrefix(string templateId, DateOnly localDate) =>
        $"[dm:{templateId}:{localDate:yyyy-MM-dd}]";

    public static bool TryParseMeta(string description, out string templateId, out DateOnly localDate)
    {
        templateId = "";
        localDate = default;
        if (string.IsNullOrEmpty(description) || !description.StartsWith("[dm:", StringComparison.Ordinal))
        {
            return false;
        }

        var end = description.IndexOf(']');
        if (end <= 4) return false;
        var inner = description[4..end]; // template:yyyy-MM-dd
        var parts = inner.Split(':');
        if (parts.Length != 2) return false;
        templateId = parts[0];
        return DateOnly.TryParse(parts[1], out localDate);
    }

}
