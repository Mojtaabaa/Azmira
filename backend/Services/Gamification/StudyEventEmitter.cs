using System.Text.Json;
using System.Text.Json.Nodes;
using KonkoorHelp.Api.Api;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class StudyEventEmitter(AppDbContext db, OutboxPublisher outbox)
{
    public async Task EmitForWriteAsync(
        WorkspaceContext workspace,
        string collection,
        JsonObject record,
        bool emitEvents,
        CancellationToken cancellationToken)
    {
        if (!emitEvents) return;

        var id = record["id"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(id)) return;

        switch (collection)
        {
            case "sessions":
                await EmitSessionAsync(workspace, id, record, cancellationToken);
                break;
            case "tests":
                await EmitTestAsync(workspace, id, record, cancellationToken);
                break;
            case "mistakes":
                await EmitReviewAsync(workspace, id, record, cancellationToken);
                break;
        }
    }

    private async Task EmitSessionAsync(WorkspaceContext workspace, string id, JsonObject record, CancellationToken cancellationToken)
    {
        var minutes = ReadInt(record, "durationMinutes");
        var subject = ReadString(record, "subject");
        await outbox.PublishAsync(
            workspace.WorkspaceId,
            workspace.UserId,
            DomainEventTypes.StudySessionCompleted,
            $"studySession:{id}:completed",
            new
            {
                sessionId = id,
                subjectId = subject,
                durationMinutes = minutes,
                source = ReadString(record, "source") is { Length: > 0 } src ? src : "timer",
            },
            cancellationToken);

        var settings = await LoadSettings(workspace, cancellationToken);
        var dayMinutes = await SumSessionMinutesForLocalDay(workspace, cancellationToken);
        if (dayMinutes >= settings.DailyGoalMinutes)
        {
            var dayKey = DateTime.UtcNow.ToString("yyyy-MM-dd");
            await outbox.PublishAsync(
                workspace.WorkspaceId,
                workspace.UserId,
                DomainEventTypes.StudyGoalCompleted,
                $"studyGoal:{workspace.UserId}:{dayKey}:daily",
                new { goalKind = "daily", minutes = dayMinutes, target = settings.DailyGoalMinutes },
                cancellationToken);
        }

        var weekMinutes = await SumSessionMinutesSince(workspace, DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek), cancellationToken);
        if (weekMinutes >= settings.WeeklyGoalMinutes)
        {
            var weekKey = $"{DateTime.UtcNow:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow):00}";
            await outbox.PublishAsync(
                workspace.WorkspaceId,
                workspace.UserId,
                DomainEventTypes.WeeklyGoalCompleted,
                $"weeklyGoal:{workspace.UserId}:{weekKey}",
                new { minutes = weekMinutes, target = settings.WeeklyGoalMinutes },
                cancellationToken);
        }
    }

    private async Task EmitTestAsync(WorkspaceContext workspace, string id, JsonObject record, CancellationToken cancellationToken)
    {
        var score = ReadDouble(record, "score");
        var subject = ReadString(record, "subject");
        await outbox.PublishAsync(
            workspace.WorkspaceId,
            workspace.UserId,
            DomainEventTypes.AssessmentSubmitted,
            $"assessment:{id}:submitted",
            new { attemptId = id, subjectId = subject, score },
            cancellationToken);

        if (score >= 80)
        {
            await outbox.PublishAsync(
                workspace.WorkspaceId,
                workspace.UserId,
                DomainEventTypes.AssessmentPassed,
                $"assessment:{id}:passed",
                new { attemptId = id, subjectId = subject, score },
                cancellationToken);
        }

        var previous = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.WorkspaceId
                && item.UserId == workspace.UserId
                && item.Collection == "tests"
                && item.ItemId != id)
            .OrderByDescending(item => item.Seq)
            .Select(item => item.Data)
            .Take(30)
            .ToListAsync(cancellationToken);

        double? prior = null;
        foreach (var raw in previous)
        {
            using var doc = JsonDocument.Parse(raw);
            var sub = doc.RootElement.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString() ?? ""
                : "";
            if (!string.Equals(sub, subject, StringComparison.OrdinalIgnoreCase)) continue;
            if (doc.RootElement.TryGetProperty("score", out var sc) && sc.TryGetDouble(out var value))
            {
                prior = value;
                break;
            }
        }

        if (prior is double previousScore && score > previousScore)
        {
            await outbox.PublishAsync(
                workspace.WorkspaceId,
                workspace.UserId,
                DomainEventTypes.MasteryImproved,
                $"mastery:{workspace.UserId}:{subject}:{id}",
                new { subjectId = subject, score, previousScore },
                cancellationToken);
        }
    }

    private async Task EmitReviewAsync(WorkspaceContext workspace, string id, JsonObject record, CancellationToken cancellationToken)
    {
        var reviewed = record["reviewed"]?.GetValue<bool>() == true
            || string.Equals(ReadString(record, "status"), "reviewed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ReadString(record, "reviewStatus"), "done", StringComparison.OrdinalIgnoreCase);
        if (!reviewed) return;

        await outbox.PublishAsync(
            workspace.WorkspaceId,
            workspace.UserId,
            DomainEventTypes.ReviewCompleted,
            $"review:{id}:completed",
            new { mistakeId = id, subjectId = ReadString(record, "subject") },
            cancellationToken);
    }

    private async Task<SettingsModel> LoadSettings(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var data = await db.WorkspaceSettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.WorkspaceId)
            .Select(item => item.Data)
            .FirstOrDefaultAsync(cancellationToken);
        if (data is null) return StudyValidation.DefaultSettings();
        return StudyValidation.NormalizeSettings(StudyValidation.ParseStored(data), false);
    }

    private async Task<int> SumSessionMinutesForLocalDay(WorkspaceContext workspace, CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow.Date;
        return await SumSessionMinutesSince(workspace, start, cancellationToken);
    }

    private async Task<int> SumSessionMinutesSince(WorkspaceContext workspace, DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var rows = await db.StudyRecords.AsNoTracking()
            .Where(item => item.WorkspaceId == workspace.WorkspaceId && item.UserId == workspace.UserId && item.Collection == "sessions")
            .Select(item => item.Data)
            .ToListAsync(cancellationToken);
        var total = 0;
        foreach (var raw in rows)
        {
            using var doc = JsonDocument.Parse(raw);
            var minutes = doc.RootElement.TryGetProperty("durationMinutes", out var m) && m.TryGetInt32(out var value) ? value : 0;
            var startText = doc.RootElement.TryGetProperty("startTime", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString()
                : null;
            if (DateTime.TryParse(startText, out var started))
            {
                var utc = started.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(started, DateTimeKind.Utc)
                    : started.ToUniversalTime();
                if (utc < sinceUtc) continue;
            }

            total += minutes;
        }

        return total;
    }

    private static int ReadInt(JsonObject record, string key)
    {
        if (record[key] is not JsonValue value) return 0;
        if (value.TryGetValue<int>(out var n)) return n;
        if (value.TryGetValue<double>(out var d)) return (int)d;
        return 0;
    }

    private static double ReadDouble(JsonObject record, string key)
    {
        if (record[key] is not JsonValue value) return 0;
        if (value.TryGetValue<double>(out var d)) return d;
        if (value.TryGetValue<int>(out var n)) return n;
        return 0;
    }

    private static string ReadString(JsonObject record, string key) =>
        record[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text ?? "" : "";
}
