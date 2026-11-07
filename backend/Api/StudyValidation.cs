using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KonkoorHelp.Api.Api;

public sealed class StudyValidationException(string message) : Exception(message);

public sealed record CustomSubject(string Id, string Name);

public sealed class SettingsModel
{
    public int PomodoroWork { get; set; } = 25;
    public int PomodoroBreak { get; set; } = 5;
    public int LongBreak { get; set; } = 15;
    public int DailyGoalMinutes { get; set; } = 240;
    public int WeeklyGoalMinutes { get; set; } = 1680;
    public string KonkoorDate { get; set; } = "";
    public List<CustomSubject> CustomSubjects { get; set; } = [];
    public List<string> HiddenSubjects { get; set; } = [];
    public Dictionary<string, int> SubjectWeights { get; set; } = new();
    public Dictionary<string, int> SubjectWeeklyGoals { get; set; } = new();
    public List<string> HiddenTopics { get; set; } = [];
    public bool SyncSessionsToPlanner { get; set; } = true;
}

public static class StudyValidation
{
    public const int MaxPerCollection = 20000;
    public const int MaxRecordBytes = 262144;

    public static readonly string[] CollectionKeys =
        ["sessions", "tests", "mistakes", "topics", "tasks", "events"];

    private static readonly string[] NumberSettings = ["pomodoroWork", "pomodoroBreak", "longBreak"];
    private static readonly HashSet<string> DefaultSubjectNames = ["ریاضی", "فیزیک", "شیمی", "هندسه", "ادبیات"];
    private static readonly HashSet<string> BuiltinSubjectIds = ["math", "physics", "chemistry", "geometry", "literature"];
    private static readonly Regex CustomSubjectId = new(@"^custom-[A-Za-z0-9-]{8,80}$", RegexOptions.Compiled);
    private static readonly Regex SubjectKey = new(@"^[A-Za-z0-9-]{1,90}$", RegexOptions.Compiled);
    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Serializer = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static SettingsModel DefaultSettings() => new();

    public static SettingsModel NormalizeSettings(JsonElement settings, bool strict)
    {
        if (settings.ValueKind != JsonValueKind.Object)
        {
            if (strict) throw new StudyValidationException("تنظیمات باید یک شیء باشد.");
            return DefaultSettings();
        }

        var next = DefaultSettings();
        foreach (var key in NumberSettings)
        {
            if (!settings.TryGetProperty(key, out var raw)) continue;
            if (!TryInt(raw, out var value) || value <= 0)
            {
                if (strict) throw new StudyValidationException($"{key} باید یک عدد درست مثبت باشد.");
                continue;
            }

            switch (key)
            {
                case "pomodoroWork": next.PomodoroWork = value; break;
                case "pomodoroBreak": next.PomodoroBreak = value; break;
                case "longBreak": next.LongBreak = value; break;
            }
        }

        if (!settings.TryGetProperty("konkoorDate", out var date) || date.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || DateText(date) == "")
        {
            next.KonkoorDate = "";
        }
        else if (IsIsoDate(DateText(date)))
        {
            next.KonkoorDate = DateText(date);
        }
        else if (strict)
        {
            throw new StudyValidationException("تاریخ کنکور باید مثل 2028-07-01 باشد.");
        }

        if (settings.TryGetProperty("dailyGoalMinutes", out var daily))
        {
            if (TryInt(daily, out var goal))
            {
                if (goal == 180) goal = 240;
                if (goal >= 15 && goal <= 1440 && goal % 15 == 0) next.DailyGoalMinutes = goal;
                else if (strict) throw new StudyValidationException("هدف روزانه باید بین ۰٫۲۵ تا ۲۴ ساعت باشد (گام ۱۵ دقیقه).");
            }
            else if (strict)
            {
                throw new StudyValidationException("هدف روزانه باید بین ۰٫۲۵ تا ۲۴ ساعت باشد (گام ۱۵ دقیقه).");
            }
        }

        if (settings.TryGetProperty("weeklyGoalMinutes", out var weekly))
        {
            if (TryInt(weekly, out var goal))
            {
                if (goal == 1260) goal = 1680;
                if (goal >= 15 && goal <= 10080 && goal % 15 == 0) next.WeeklyGoalMinutes = goal;
                else if (strict) throw new StudyValidationException("هدف هفته باید بین ۰٫۲۵ تا ۱۶۸ ساعت باشد (گام ۱۵ دقیقه).");
            }
            else if (strict)
            {
                throw new StudyValidationException("هدف هفته باید بین ۰٫۲۵ تا ۱۶۸ ساعت باشد (گام ۱۵ دقیقه).");
            }
        }

        if (!settings.TryGetProperty("syncSessionsToPlanner", out var sync) || sync.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            next.SyncSessionsToPlanner = true;
        }
        else if (sync.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            next.SyncSessionsToPlanner = sync.GetBoolean();
        }
        else if (strict)
        {
            throw new StudyValidationException("همگام‌سازی زمان‌سنج و برنامه باید بولین باشد.");
        }
        else
        {
            next.SyncSessionsToPlanner = true;
        }

        next.CustomSubjects = NormalizeCustomSubjects(Property(settings, "customSubjects"), strict);
        next.HiddenSubjects = NormalizeHiddenSubjects(Property(settings, "hiddenSubjects"), strict);
        next.SubjectWeights = NormalizeWeights(Property(settings, "subjectWeights"), strict);
        next.SubjectWeeklyGoals = NormalizeWeeklyGoals(Property(settings, "subjectWeeklyGoals"), strict);
        next.HiddenTopics = NormalizeHiddenTopics(Property(settings, "hiddenTopics"), strict);
        return next;
    }

    public static List<JsonObject> NormalizeCollection(string name, JsonElement items, bool strict)
    {
        var label = Label(name);
        if (items.ValueKind != JsonValueKind.Array)
        {
            if (strict) throw new StudyValidationException($"{label} باید یک فهرست باشد.");
            return [];
        }

        var seen = new HashSet<string>();
        var next = new List<JsonObject>();
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            index += 1;
            if (item.ValueKind != JsonValueKind.Object)
            {
                if (strict) throw new StudyValidationException($"مورد {index} در {label} باید یک شیء باشد.");
                continue;
            }

            var id = item.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                ? (idElement.GetString() ?? "").Trim()
                : "";
            if (id.Length == 0)
            {
                if (strict) throw new StudyValidationException($"مورد {index} در {label} شناسه ندارد.");
                id = Guid.NewGuid().ToString();
            }

            if (!seen.Add(id))
            {
                if (strict) throw new StudyValidationException($"{label} شناسه تکراری دارد.");
                continue;
            }

            var copy = JsonNode.Parse(item.GetRawText()) as JsonObject ?? new JsonObject();
            copy["id"] = id;
            next.Add(copy);
        }

        return next;
    }

    public static (Dictionary<string, List<JsonObject>> Collections, SettingsModel Settings) NormalizeData(JsonElement incoming, bool strict)
    {
        if (incoming.ValueKind != JsonValueKind.Object)
        {
            if (strict) throw new StudyValidationException("فایل پشتیبان باید یک شیء JSON باشد.");
            return (EmptyCollections(), DefaultSettings());
        }

        var data = new Dictionary<string, List<JsonObject>>();
        foreach (var key in CollectionKeys)
        {
            if (strict && !incoming.TryGetProperty(key, out _))
            {
                throw new StudyValidationException($"فایل پشتیبان «{key}» را ندارد.");
            }

            var items = incoming.TryGetProperty(key, out var value) ? value : default;
            data[key] = NormalizeCollection(key, items, strict);
        }

        var settings = incoming.TryGetProperty("settings", out var settingsElement) ? settingsElement : default;
        return (data, NormalizeSettings(settings, strict));
    }

    public static string SerializeSettings(SettingsModel settings) => JsonSerializer.Serialize(settings, Serializer);

    public static JsonElement ParseStored(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return document.RootElement.Clone();
    }

    private static List<CustomSubject> NormalizeCustomSubjects(JsonElement value, bool strict)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            if (strict) throw new StudyValidationException("درس‌های سفارشی باید یک فهرست باشد.");
            return [];
        }

        var names = new HashSet<string>(DefaultSubjectNames);
        var ids = new HashSet<string>();
        var next = new List<CustomSubject>();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            index += 1;
            var place = $"درس سفارشی {index}";
            if (item.ValueKind != JsonValueKind.Object)
            {
                if (strict) throw new StudyValidationException($"{place} باید یک شیء باشد.");
                continue;
            }

            var id = Text(item, "id").Trim();
            var name = Regex.Replace(Text(item, "name").Trim(), @"\s+", " ");
            if (!CustomSubjectId.IsMatch(id))
            {
                if (strict) throw new StudyValidationException($"{place} شناسه درستی ندارد.");
                continue;
            }

            if (name.Length == 0 || name.Length > 40)
            {
                if (strict) throw new StudyValidationException($"{place} نام درستی ندارد.");
                continue;
            }

            if (!names.Add(name) || !ids.Add(id))
            {
                if (strict) throw new StudyValidationException($"{place} تکراری است.");
                continue;
            }

            next.Add(new CustomSubject(id, name));
        }

        return next;
    }

    private static Dictionary<string, int> NormalizeWeights(JsonElement value, bool strict)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return new();
        if (value.ValueKind != JsonValueKind.Object)
        {
            if (strict) throw new StudyValidationException("وزن درس‌ها باید یک شیء باشد.");
            return new();
        }

        var next = new Dictionary<string, int>();
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name is "__proto__" or "constructor" or "prototype") continue;
            if (!SubjectKey.IsMatch(property.Name) || !TryInt(property.Value, out var weight) || weight < 0 || weight > 100)
            {
                if (strict) throw new StudyValidationException("وزن هر درس باید یک عدد از 0 تا 100 باشد.");
                continue;
            }

            next[property.Name] = weight;
        }

        return next;
    }

    private static Dictionary<string, int> NormalizeWeeklyGoals(JsonElement value, bool strict)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return new();
        if (value.ValueKind != JsonValueKind.Object)
        {
            if (strict) throw new StudyValidationException("هدف هفتگی درس‌ها باید یک شیء باشد.");
            return new();
        }

        var next = new Dictionary<string, int>();
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name is "__proto__" or "constructor" or "prototype") continue;
            if (!SubjectKey.IsMatch(property.Name) || !TryInt(property.Value, out var minutes) || minutes < 15 || minutes > 10080 || minutes % 15 != 0)
            {
                if (strict) throw new StudyValidationException("هدف هفتگی هر درس باید بین ۰٫۲۵ تا ۱۶۸ ساعت باشد (گام ۱۵ دقیقه).");
                continue;
            }

            next[property.Name] = minutes;
        }

        return next;
    }

    private static List<string> NormalizeHiddenSubjects(JsonElement value, bool strict)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            if (strict) throw new StudyValidationException("درس‌های پنهان باید یک فهرست باشد.");
            return [];
        }

        var next = new List<string>();
        var seen = new HashSet<string>();
        foreach (var item in value.EnumerateArray())
        {
            var id = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : "";
            if (!BuiltinSubjectIds.Contains(id) || !seen.Add(id))
            {
                if (strict) throw new StudyValidationException("شناسه درس پنهان درست نیست.");
                continue;
            }

            next.Add(id);
        }

        return next;
    }

    private static List<string> NormalizeHiddenTopics(JsonElement value, bool strict)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array)
        {
            if (strict) throw new StudyValidationException("مبحث‌های حذف‌شده باید یک فهرست باشد.");
            return [];
        }

        var next = new List<string>();
        var seen = new HashSet<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !(item.GetString() ?? "").Contains("::", StringComparison.Ordinal))
            {
                if (strict) throw new StudyValidationException("شناسه مبحث حذف‌شده درست نیست.");
                continue;
            }

            var key = (item.GetString() ?? "").Trim();
            if (key.Length == 0 || !seen.Add(key)) continue;
            next.Add(key);
        }

        return next;
    }

    private static Dictionary<string, List<JsonObject>> EmptyCollections() =>
        CollectionKeys.ToDictionary(key => key, _ => new List<JsonObject>());

    private static string Label(string name) => name switch
    {
        "sessions" => "جلسه‌ها",
        "tests" => "آزمون‌ها",
        "mistakes" => "اشتباهات",
        "topics" => "مبحث‌ها",
        "tasks" => "کارها",
        "events" => "رویدادها",
        _ => name,
    };

    private static JsonElement Property(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) ? value : default;

    private static string Text(JsonElement owner, string name) =>
        owner.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string DateText(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static bool TryInt(JsonElement value, out int number)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out number)) return true;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = 0;
        return false;
    }

    private static bool IsIsoDate(string value)
    {
        if (!DatePattern.IsMatch(value)) return false;
        var parts = value.Split('-');
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        var day = int.Parse(parts[2], CultureInfo.InvariantCulture);
        try
        {
            var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            return date.Year == year && date.Month == month && date.Day == day;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
