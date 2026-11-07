using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class StreakEngine(AppDbContext db, GamificationProfileService profiles)
{
    public const string ConsumerName = "StreakEngine";

    public async Task ProcessAsync(DomainOutbox row, DomainEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (await db.ProcessedEvents.AnyAsync(item => item.ConsumerName == ConsumerName && item.EventId == row.Id, cancellationToken))
        {
            return;
        }

        if (envelope.ActorUserId is not int userId || envelope.EventType != DomainEventTypes.StudySessionCompleted)
        {
            await Mark(row.Id, cancellationToken);
            return;
        }

        var profile = await profiles.ResolveAsync(row.WorkspaceId, cancellationToken);
        var streakSettings = profiles.ParseStreak(profile);
        var tzName = await profiles.ResolveTimeZoneAsync(row.WorkspaceId, cancellationToken);
        var localDate = ToLocalDate(envelope.OccurredAt, tzName);
        var minutes = EventData.ReadInt(envelope.Data, "durationMinutes");

        var stats = await GetOrCreateStatsAsync(row.WorkspaceId, userId, tzName, streakSettings, cancellationToken);
        var day = await db.StreakDays.FirstOrDefaultAsync(
            item => item.WorkspaceId == row.WorkspaceId && item.UserId == userId && item.LocalDate == localDate,
            cancellationToken);

        if (day is null)
        {
            day = new StreakDay
            {
                Id = Guid.NewGuid(),
                WorkspaceId = row.WorkspaceId,
                UserId = userId,
                LocalDate = localDate,
                Kind = StreakDayKind.Study,
                Minutes = minutes,
                CreatedAt = DateTime.UtcNow,
            };
            db.StreakDays.Add(day);
        }
        else
        {
            day.Minutes += minutes;
            if (day.Kind != StreakDayKind.Study && day.Minutes >= streakSettings.MinMinutesPerDay)
            {
                day.Kind = StreakDayKind.Study;
            }
        }

        if (day.Minutes >= streakSettings.MinMinutesPerDay)
        {
            RecalculateStreak(stats, localDate, streakSettings);
            await MaybeMilestoneAsync(row.WorkspaceId, userId, stats, streakSettings, cancellationToken);
        }

        stats.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await Mark(row.Id, cancellationToken);
    }

    public async Task SetRestDayAsync(Guid workspaceId, int userId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var profile = await profiles.ResolveAsync(workspaceId, cancellationToken);
        var settings = profiles.ParseStreak(profile);
        var weekStart = localDate.AddDays(-(int)localDate.DayOfWeek);
        var restCount = await db.StreakDays.CountAsync(
            item => item.WorkspaceId == workspaceId
                && item.UserId == userId
                && item.Kind == StreakDayKind.Rest
                && item.LocalDate >= weekStart
                && item.LocalDate < weekStart.AddDays(7),
            cancellationToken);
        if (restCount >= settings.WeeklyRestAllowance)
        {
            throw new InvalidOperationException("سهمیه روز استراحت این هفته تمام شده.");
        }

        var existing = await db.StreakDays.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId && item.LocalDate == localDate,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.Kind == StreakDayKind.Study && existing.Minutes >= settings.MinMinutesPerDay)
            {
                throw new InvalidOperationException("این روز قبلاً به‌عنوان مطالعه ثبت شده.");
            }

            existing.Kind = StreakDayKind.Rest;
        }
        else
        {
            db.StreakDays.Add(new StreakDay
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                LocalDate = localDate,
                Kind = StreakDayKind.Rest,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UseFreezeAsync(Guid workspaceId, int userId, DateOnly localDate, CancellationToken cancellationToken)
    {
        var tz = await profiles.ResolveTimeZoneAsync(workspaceId, cancellationToken);
        var profile = await profiles.ResolveAsync(workspaceId, cancellationToken);
        var settings = profiles.ParseStreak(profile);
        var stats = await GetOrCreateStatsAsync(workspaceId, userId, tz, settings, cancellationToken);
        if (stats.StreakFreezes <= 0) throw new InvalidOperationException("یخ‌زدگی استریک باقی نمانده.");

        var existing = await db.StreakDays.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId && item.LocalDate == localDate,
            cancellationToken);
        if (existing is not null && existing.Kind == StreakDayKind.Study)
        {
            throw new InvalidOperationException("این روز مطالعه دارد و نیازی به فریز نیست.");
        }

        if (existing is null)
        {
            db.StreakDays.Add(new StreakDay
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                LocalDate = localDate,
                Kind = StreakDayKind.Freeze,
                CreatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.Kind = StreakDayKind.Freeze;
        }

        stats.StreakFreezes -= 1;
        stats.UpdatedAt = DateTime.UtcNow;
        RecalculateStreak(stats, localDate, settings);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static DateOnly ToLocalDate(DateTime utc, string timeZoneId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(NormalizeTz(timeZoneId));
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
            return DateOnly.FromDateTime(local);
        }
        catch (TimeZoneNotFoundException)
        {
            var offset = timeZoneId is "Asia/Tehran" ? TimeSpan.FromHours(3.5) : TimeSpan.Zero;
            return DateOnly.FromDateTime(utc + offset);
        }
    }

    private static string NormalizeTz(string timeZoneId) =>
        timeZoneId switch
        {
            "Asia/Tehran" => OperatingSystem.IsWindows() ? "Iran Standard Time" : "Asia/Tehran",
            _ => timeZoneId,
        };

    private void RecalculateStreak(UserGameStats stats, DateOnly today, StreakSettings settings)
    {
        var cursor = today;
        var count = 0;
        while (true)
        {
            var day = db.StreakDays.AsNoTracking().FirstOrDefault(item =>
                item.WorkspaceId == stats.WorkspaceId
                && item.UserId == stats.UserId
                && item.LocalDate == cursor);
            if (day is null) break;

            var ok = day.Kind switch
            {
                StreakDayKind.Study => day.Minutes >= settings.MinMinutesPerDay,
                StreakDayKind.Rest => true,
                StreakDayKind.Freeze => true,
                _ => false,
            };
            if (!ok) break;

            count++;
            cursor = cursor.AddDays(-1);
        }

        stats.StreakCurrent = count;
        if (count > stats.StreakBest) stats.StreakBest = count;
        stats.LastStudyLocalDate = today;
    }

    private async Task MaybeMilestoneAsync(
        Guid workspaceId,
        int userId,
        UserGameStats stats,
        StreakSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.MilestoneDays.Contains(stats.StreakCurrent)) return;
        db.GameNotifications.Add(new GameNotification
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Kind = GameNotificationKind.StreakMilestone,
            Title = "رکورد استریک",
            Body = $"{stats.StreakCurrent} روز پیاپی!",
            PayloadJson = JsonSerializer.Serialize(new { streak = stats.StreakCurrent }, GamificationJson.Options),
            CreatedAt = DateTime.UtcNow,
        });
        if (settings.MilestoneXp > 0)
        {
            stats.XpTotal += settings.MilestoneXp;
            db.XpLedger.Add(new XpLedger
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                EventId = null,
                Delta = settings.MilestoneXp,
                BalanceAfter = stats.XpTotal,
                Reason = $"streak_milestone_{stats.StreakCurrent}",
                RuleCode = "streak_milestone",
                Kind = LedgerKind.Earn,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await Task.CompletedTask;
    }

    private async Task<UserGameStats> GetOrCreateStatsAsync(
        Guid workspaceId,
        int userId,
        string tz,
        StreakSettings settings,
        CancellationToken cancellationToken)
    {
        var stats = await db.UserGameStats.FirstOrDefaultAsync(
            item => item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken);
        if (stats is not null) return stats;
        stats = new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Level = 1,
            StreakFreezes = settings.StartingFreezes,
            TimeZone = tz,
            UpdatedAt = DateTime.UtcNow,
        };
        db.UserGameStats.Add(stats);
        await db.SaveChangesAsync(cancellationToken);
        return stats;
    }

    private async Task Mark(Guid eventId, CancellationToken cancellationToken)
    {
        if (await db.ProcessedEvents.AnyAsync(item => item.ConsumerName == ConsumerName && item.EventId == eventId, cancellationToken))
        {
            return;
        }

        db.ProcessedEvents.Add(new ProcessedEvent { ConsumerName = ConsumerName, EventId = eventId, ProcessedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(cancellationToken);
    }

}
