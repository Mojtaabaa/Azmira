using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public static class DefaultGamificationSeed
{
    public const string DefaultProfileCode = "default";

    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var profile = await db.GamificationProfiles.FirstOrDefaultAsync(item => item.Code == DefaultProfileCode, cancellationToken);
        if (profile is null)
        {
            var now = DateTime.UtcNow;
            profile = new GamificationProfile
            {
                Id = Guid.NewGuid(),
                Code = DefaultProfileCode,
                Name = "پروفایل پیش‌فرض",
                IsSystem = true,
                RulesJson = JsonSerializer.Serialize(DefaultRules(), GamificationJson.Options),
                LevelsJson = JsonSerializer.Serialize(DefaultLevels(), GamificationJson.Options),
                StreakJson = JsonSerializer.Serialize(DefaultStreak(), GamificationJson.Options),
                DailyXpCap = 200,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.GamificationProfiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureBadgesAsync(db, profile.Id, cancellationToken);
        await EnsureDefaultQuestsAsync(db, cancellationToken);

        var unbound = await db.WorkspaceSettings
            .Where(item => item.GamificationProfileId == null)
            .ToListAsync(cancellationToken);
        foreach (var settings in unbound)
        {
            settings.GamificationProfileId = profile.Id;
            if (string.IsNullOrWhiteSpace(settings.FeatureFlags) || settings.FeatureFlags == "{}")
            {
                settings.FeatureFlags = """{"gamification":true}""";
            }
            settings.UpdatedAt = DateTime.UtcNow;
        }

        if (unbound.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }

    public static ProfileRulesDocument DefaultRules() => new()
    {
        Version = 1,
        Awards =
        [
            new XpAwardRule { Code = "study_30m", On = DomainEventTypes.StudySessionCompleted, Xp = 10, PerMinutes = 30, MaxPerDay = 200 },
            new XpAwardRule { Code = "daily_goal", On = DomainEventTypes.StudyGoalCompleted, Xp = 20 },
            new XpAwardRule { Code = "assignment", On = DomainEventTypes.AssignmentCompleted, Xp = 15 },
            new XpAwardRule { Code = "review", On = DomainEventTypes.ReviewCompleted, Xp = 10 },
            new XpAwardRule { Code = "assessment_submit", On = DomainEventTypes.AssessmentSubmitted, Xp = 10 },
            new XpAwardRule { Code = "assessment_80", On = DomainEventTypes.AssessmentPassed, Xp = 20, MinScore = 80 },
            new XpAwardRule { Code = "improvement", On = DomainEventTypes.MasteryImproved, Xp = 20 },
            new XpAwardRule { Code = "weekly_goal", On = DomainEventTypes.WeeklyGoalCompleted, Xp = 50 },
            new XpAwardRule { Code = "quest", On = DomainEventTypes.QuestCompleted, Xp = 0 },
        ],
    };

    public static List<LevelDefinition> DefaultLevels() =>
    [
        new() { Level = 1, Title = "تازه‌کار", TitleEn = "Beginner", RequiredTotalXp = 0, Icon = "seed" },
        new() { Level = 2, Title = "کاشف", TitleEn = "Explorer", RequiredTotalXp = 100, Icon = "compass" },
        new() { Level = 3, Title = "تلاشگر", TitleEn = "Hard Worker", RequiredTotalXp = 300, Icon = "flame" },
        new() { Level = 4, Title = "متعهد", TitleEn = "Dedicated", RequiredTotalXp = 600, Icon = "shield" },
        new() { Level = 5, Title = "حرفه‌ای", TitleEn = "Professional", RequiredTotalXp = 1000, Icon = "star" },
        new() { Level = 6, Title = "استاد یادگیری", TitleEn = "Learning Master", RequiredTotalXp = 1800, Icon = "crown" },
        new() { Level = 7, Title = "نابغه", TitleEn = "Genius", RequiredTotalXp = 3000, Icon = "spark" },
    ];

    public static StreakSettings DefaultStreak() => new();

    private static async Task EnsureBadgesAsync(AppDbContext db, Guid profileId, CancellationToken cancellationToken)
    {
        var existing = await db.Badges.AsNoTracking()
            .Where(item => item.ProfileId == profileId)
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        foreach (var badge in DefaultBadges(profileId, now))
        {
            if (set.Contains(badge.Code)) continue;
            db.Badges.Add(badge);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<BadgeDefinition> DefaultBadges(Guid profileId, DateTime now)
    {
        yield return Badge(profileId, "first_study", "اولین مطالعه", "اولین جلسه مطالعه را ثبت کردی.", BadgeTier.Bronze, false, """{"on":"StudySessionCompleted","minCount":1}""", now);
        yield return Badge(profileId, "seven_day_streak", "هفت روز پیاپی", "هفت روز پشت‌سرهم مطالعه کردی.", BadgeTier.Silver, false, """{"streakDays":7}""", now);
        yield return Badge(profileId, "thirty_day_streak", "سی روز پیاپی", "سی روز پشت‌سرهم مطالعه کردی.", BadgeTier.Gold, false, """{"streakDays":30}""", now);
        yield return Badge(profileId, "study_master", "استاد مطالعه", "مجموع ۱۰۰۰ دقیقه مطالعه مؤثر.", BadgeTier.Gold, false, """{"totalStudyMinutes":1000}""", now);
        yield return Badge(profileId, "math_master", "استاد ریاضی", "پنج آزمون ریاضی با نمره بالای ۸۰.", BadgeTier.Silver, false, """{"subject":"math","minScore":80,"minCount":5}""", now);
        yield return Badge(profileId, "science_master", "استاد علوم", "پنج آزمون فیزیک یا شیمی با نمره بالای ۸۰.", BadgeTier.Silver, false, """{"subjects":["physics","chemistry"],"minScore":80,"minCount":5}""", now);
        yield return Badge(profileId, "review_expert", "خبره مرور", "ده مرور کامل اشتباهات.", BadgeTier.Bronze, false, """{"on":"ReviewCompleted","minCount":10}""", now);
        yield return Badge(profileId, "perfect_week", "هفته کامل", "هفت روز متوالی هدف روزانه را زدی.", BadgeTier.Gold, false, """{"dailyGoalsInRow":7}""", now);
        yield return Badge(profileId, "improvement_champion", "قهرمان پیشرفت", "سه بار بهبود نسبت به آزمون قبل.", BadgeTier.Silver, false, """{"on":"MasteryImproved","minCount":3}""", now);
        yield return Badge(profileId, "consistency_champion", "قهرمان پیوستگی", "بهترین رکورد استریک ۳۰ روز.", BadgeTier.Diamond, true, """{"bestStreak":30}""", now);
    }

    private static BadgeDefinition Badge(
        Guid profileId,
        string code,
        string title,
        string description,
        BadgeTier tier,
        bool hidden,
        string conditions,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        ProfileId = profileId,
        Code = code,
        Title = title,
        Description = description,
        Tier = tier,
        Icon = code,
        IsHidden = hidden,
        AllowRepeat = false,
        ConditionsJson = conditions,
        CreatedAt = now,
    };

    private static async Task EnsureDefaultQuestsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Quests.AnyAsync(item => item.WorkspaceId == null && item.Kind == QuestKind.Daily, cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Quests.AddRange(
            new QuestDefinition
            {
                Id = Guid.NewGuid(),
                Title = "مطالعه امروز",
                Description = "امروز حداقل ۶۰ دقیقه مطالعه مؤثر داشته باش.",
                Category = "study",
                Kind = QuestKind.Daily,
                TargetValue = 60,
                RewardXp = 15,
                RewardCoins = 5,
                CompletionCondition = "study_minutes",
                Visibility = QuestVisibility.Public,
                Difficulty = 1,
                Status = QuestStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            },
            new QuestDefinition
            {
                Id = Guid.NewGuid(),
                Title = "هفته پربار",
                Description = "این هفته ۵ جلسه مطالعه ثبت کن.",
                Category = "study",
                Kind = QuestKind.Weekly,
                TargetValue = 5,
                RewardXp = 40,
                RewardCoins = 15,
                CompletionCondition = "session_count",
                Visibility = QuestVisibility.Public,
                Difficulty = 2,
                Status = QuestStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
            });
        await db.SaveChangesAsync(cancellationToken);
    }
}
