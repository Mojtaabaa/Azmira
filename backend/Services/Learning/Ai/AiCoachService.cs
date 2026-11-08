using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Learning.Ai;

public sealed class AiCoachService(
    AppDbContext db,
    AiContextBuilder context,
    IAiProviderResolver providers,
    WebhookService webhooks)
{
    private const string SystemPrompt =
        "You are a learning coach. Use only the provided facts JSON. Never invent personal identifiers. "
        + "Respond in Persian when the user context is Persian. Separate facts from recommendations. Return valid JSON.";

    public async Task<object> AnalyzeAsync(Guid workspaceId, int actorUserId, int? targetUserId, CancellationToken cancellationToken)
    {
        var userId = targetUserId ?? actorUserId;
        await context.EnsureAuthorizedAsync(workspaceId, actorUserId, userId, cancellationToken);
        var settings = await RequireEnabledAsync(workspaceId, userId, cancellationToken);
        var facts = await context.BuildFactsAsync(workspaceId, userId, cancellationToken);
        var provider = providers.Resolve(settings.ProviderCode);

        var userPrompt = JsonSerializer.Serialize(new
        {
            kind = "coach",
            facts,
        });

        var completion = await provider.CompleteAsync(
            new AiCompletionRequest(settings.Model, SystemPrompt + " " + settings.SystemPromptExtra, userPrompt),
            cancellationToken);
        if (!completion.Ok)
            throw new InvalidOperationException(completion.Error ?? "سرویس هوش مصنوعی در دسترس نیست.");

        var parsed = TryParseObject(completion.Text) ?? new Dictionary<string, object?>
        {
            ["analysis"] = completion.Text,
            ["recommendations"] = Array.Empty<object>(),
        };

        var recommendations = new List<object>();
        if (parsed.TryGetValue("recommendations", out var recObj) && recObj is JsonElement recEl && recEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in recEl.EnumerateArray())
            {
                var kind = Enum.TryParse<AiRecommendationKind>(item.TryGetProperty("kind", out var k) ? k.GetString() : "StudyPlan", true, out var parsedKind)
                    ? parsedKind
                    : AiRecommendationKind.StudyPlan;
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "پیشنهاد" : "پیشنهاد";
                var body = item.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                var row = new AiRecommendation
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Kind = kind,
                    Status = AiRecommendationStatus.Suggested,
                    Title = title,
                    Body = body,
                    PayloadJson = item.GetRawText(),
                    FactsJson = JsonSerializer.Serialize(facts),
                    ProviderCode = completion.ProviderCode,
                    CreatedAt = DateTime.UtcNow,
                };
                db.AiRecommendations.Add(row);
                recommendations.Add(new { id = row.Id, kind = row.Kind.ToString(), title = row.Title, body = row.Body, status = row.Status.ToString() });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            facts,
            analysis = parsed.TryGetValue("analysis", out var a) ? a : null,
            recommendations,
            provider = completion.ProviderCode,
            note = "facts = داده واقعی برنامه؛ recommendations = تولیدشده توسط هوش مصنوعی و نیازمند تأیید.",
        };
    }

    public async Task<object> WeeklyReportAsync(Guid workspaceId, int actorUserId, int? targetUserId, CancellationToken cancellationToken)
    {
        var userId = targetUserId ?? actorUserId;
        await context.EnsureAuthorizedAsync(workspaceId, actorUserId, userId, cancellationToken);
        var settings = await RequireEnabledAsync(workspaceId, userId, cancellationToken);
        var facts = await context.BuildFactsAsync(workspaceId, userId, cancellationToken);
        var periodKey = $"{DateTime.UtcNow:yyyy}-W{System.Globalization.ISOWeek.GetWeekOfYear(DateTime.UtcNow):00}";

        var existing = await db.AiWeeklyReports.AsNoTracking()
            .FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId && item.PeriodKey == periodKey, cancellationToken);
        if (existing is not null)
        {
            return new
            {
                id = existing.Id,
                periodKey,
                facts = JsonSerializer.Deserialize<object>(existing.FactsJson),
                narrative = existing.Narrative,
                recommendations = JsonSerializer.Deserialize<object>(existing.RecommendationsJson),
                provider = existing.ProviderCode,
                cached = true,
            };
        }

        var provider = providers.Resolve(settings.ProviderCode);
        var prompt = JsonSerializer.Serialize(new { kind = "weekly_report", facts });
        var completion = await provider.CompleteAsync(
            new AiCompletionRequest(settings.Model, SystemPrompt + " " + settings.SystemPromptExtra, prompt),
            cancellationToken);
        if (!completion.Ok)
            throw new InvalidOperationException(completion.Error ?? "سرویس هوش مصنوعی در دسترس نیست.");

        var parsed = TryParseObject(completion.Text);
        var narrative = parsed is not null && parsed.TryGetValue("narrative", out var n) ? n?.ToString() ?? completion.Text : completion.Text;
        var recJson = parsed is not null && parsed.TryGetValue("recommendations", out var r)
            ? JsonSerializer.Serialize(r)
            : "[]";

        var report = new AiWeeklyReport
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Kind = AiReportKind.Weekly,
            PeriodKey = periodKey,
            FactsJson = JsonSerializer.Serialize(facts),
            Narrative = narrative ?? "",
            RecommendationsJson = recJson,
            ProviderCode = completion.ProviderCode,
            GeneratedAt = DateTime.UtcNow,
        };
        db.AiWeeklyReports.Add(report);
        await db.SaveChangesAsync(cancellationToken);
        await webhooks.EnqueueForEventAsync(
            workspaceId,
            report.Id,
            WebhookEventTypes.WeeklyReportGenerated,
            new { reportId = report.Id, userId, periodKey },
            cancellationToken);

        return new
        {
            id = report.Id,
            periodKey,
            facts,
            narrative = report.Narrative,
            recommendations = JsonSerializer.Deserialize<object>(report.RecommendationsJson),
            provider = report.ProviderCode,
            cached = false,
            note = "facts از داده واقعی استخراج شده؛ narrative و recommendations تولید هوش مصنوعی هستند.",
        };
    }

    public async Task<object> SuggestPlanAsync(
        Guid workspaceId,
        int actorUserId,
        int availableMinutes,
        string? commitmentsJson,
        CancellationToken cancellationToken)
    {
        await context.EnsureAuthorizedAsync(workspaceId, actorUserId, actorUserId, cancellationToken);
        var settings = await RequireEnabledAsync(workspaceId, actorUserId, cancellationToken);
        var factsObj = await context.BuildFactsAsync(workspaceId, actorUserId, cancellationToken);
        var factsJson = JsonSerializer.Serialize(factsObj);
        using var factsDoc = JsonDocument.Parse(factsJson);
        var factsRoot = factsDoc.RootElement.Clone();

        var provider = providers.Resolve(settings.ProviderCode);
        var prompt = JsonSerializer.Serialize(new
        {
            kind = "planner",
            availableMinutes,
            commitments = string.IsNullOrWhiteSpace(commitmentsJson) ? "[]" : commitmentsJson,
            facts = factsRoot,
        });
        var completion = await provider.CompleteAsync(
            new AiCompletionRequest(settings.Model, SystemPrompt + " " + settings.SystemPromptExtra, prompt),
            cancellationToken);
        if (!completion.Ok)
            throw new InvalidOperationException(completion.Error ?? "سرویس هوش مصنوعی در دسترس نیست.");

        var parsed = TryParseObject(completion.Text);
        var title = parsed is not null && parsed.TryGetValue("title", out var t) ? t?.ToString() ?? "برنامه پیشنهادی" : "برنامه پیشنهادی";
        var scheduleJson = parsed is not null && parsed.TryGetValue("schedule", out var s)
            ? JsonSerializer.Serialize(s)
            : "[]";

        var plan = new AiPlanSuggestion
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = actorUserId,
            Status = AiRecommendationStatus.Suggested,
            Title = title ?? "برنامه پیشنهادی",
            ScheduleJson = scheduleJson,
            FactsJson = factsJson,
            ProviderCode = completion.ProviderCode,
            CreatedAt = DateTime.UtcNow,
        };
        db.AiPlanSuggestions.Add(plan);
        await db.SaveChangesAsync(cancellationToken);

        return new
        {
            id = plan.Id,
            title = plan.Title,
            status = plan.Status.ToString(),
            schedule = JsonSerializer.Deserialize<object>(plan.ScheduleJson),
            facts = factsObj,
            provider = plan.ProviderCode,
            note = "پیشنهاد قابل ویرایش است و بدون تأیید کاربر اعمال نمی‌شود.",
        };
    }

    public async Task<object> ChatAsync(
        Guid workspaceId,
        int userId,
        string message,
        Guid? threadId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("پیام خالی است.");

        await context.EnsureAuthorizedAsync(workspaceId, userId, userId, cancellationToken);
        var settings = await RequireEnabledAsync(workspaceId, userId, cancellationToken);
        var facts = await context.BuildFactsAsync(workspaceId, userId, cancellationToken);

        CoachThread thread;
        if (threadId is Guid existingId)
        {
            thread = await db.CoachThreads
                .Include(item => item.Messages)
                .FirstOrDefaultAsync(
                    item => item.Id == existingId && item.WorkspaceId == workspaceId && item.UserId == userId,
                    cancellationToken)
                ?? throw new InvalidOperationException("گفتگو پیدا نشد.");
        }
        else
        {
            thread = new CoachThread
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.CoachThreads.Add(thread);
        }

        var userMsg = new CoachMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            Role = "user",
            Content = message.Trim(),
            Confidence = "",
            EvidenceRefsJson = "[]",
            ProviderCode = "",
            CreatedAt = DateTime.UtcNow,
        };
        db.CoachMessages.Add(userMsg);

        var evidenceRefs = BuildEvidenceRefs(facts);
        var confidence = evidenceRefs.Count >= 3 ? "high" : evidenceRefs.Count >= 1 ? "medium" : "low";

        var provider = providers.Resolve(settings.ProviderCode);
        var prompt = JsonSerializer.Serialize(new
        {
            kind = "chat",
            message = message.Trim(),
            facts,
            evidenceRefs,
            confidence,
        });
        var completion = await provider.CompleteAsync(
            new AiCompletionRequest(settings.Model, SystemPrompt + " " + settings.SystemPromptExtra, prompt),
            cancellationToken);

        // evidenceRefs always server-built from tenant facts — never accept provider-supplied refs.
        var replyRefs = evidenceRefs;
        string reply;
        var replyConfidence = confidence;

        if (completion.Ok)
        {
            var parsed = TryParseObject(completion.Text);
            if (parsed is not null && parsed.TryGetValue("reply", out var r) && r is not null)
            {
                reply = r.ToString() ?? completion.Text;
            }
            else
            {
                reply = string.IsNullOrWhiteSpace(completion.Text)
                    ? HeuristicChatReply(facts, evidenceRefs, confidence)
                    : completion.Text;
            }

            // Provider may only lower confidence, never inflate beyond server-derived evidence.
            if (parsed is not null && parsed.TryGetValue("confidence", out var conf) && conf is not null)
            {
                var c = conf.ToString()?.Trim().ToLowerInvariant();
                if (c is "high" or "medium" or "low")
                    replyConfidence = MinConfidence(confidence, c);
            }
        }
        else
        {
            // Heuristic fallback when LLM/provider fails — core UX must not require a key.
            reply = HeuristicChatReply(facts, evidenceRefs, confidence);
            completion = new AiCompletionResult(true, reply, null, "heuristic");
        }

        if (replyConfidence == "low" && evidenceRefs.Count == 0)
        {
            reply = "داده کافی برای پاسخ مطمئن نیست. بعد از چند ارزیابی یا بازبینی موفق دوباره بپرس.";
        }

        var assistantMsg = new CoachMessage
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            Role = "assistant",
            Content = reply,
            Confidence = replyConfidence,
            EvidenceRefsJson = JsonSerializer.Serialize(replyRefs),
            ProviderCode = completion.ProviderCode,
            CreatedAt = DateTime.UtcNow,
        };
        db.CoachMessages.Add(assistantMsg);
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new
        {
            threadId = thread.Id,
            messageId = assistantMsg.Id,
            reply,
            confidence = replyConfidence,
            confidenceNote = "high|medium|low از حجم شواهد مجاز است، نه قطعیت آماری.",
            evidenceRefs = replyRefs,
            evidencePolicy = "serverFactsOnly",
            provider = completion.ProviderCode,
            note = "facts از داده واقعی فضای کاری؛ narrative پیشنهادی است و بدون تأیید شما تغییری اعمال نمی‌شود.",
        };
    }

    private static string MinConfidence(string a, string b)
    {
        static int Rank(string value) => value switch
        {
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 1,
        };
        return Rank(a) <= Rank(b) ? a : b;
    }

    public async Task<object> ResolveRecommendationAsync(
        Guid workspaceId,
        int userId,
        Guid id,
        bool approve,
        string? editedPayloadJson,
        CancellationToken cancellationToken)
    {
        var row = await db.AiRecommendations.FirstOrDefaultAsync(
            item => item.Id == id && item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken) ?? throw new InvalidOperationException("پیشنهاد پیدا نشد.");

        if (!string.IsNullOrWhiteSpace(editedPayloadJson)) row.PayloadJson = editedPayloadJson;
        row.Status = approve ? AiRecommendationStatus.Approved : AiRecommendationStatus.Rejected;
        row.ResolvedAt = DateTime.UtcNow;
        row.ResolvedByUserId = userId;
        await db.SaveChangesAsync(cancellationToken);
        return new { id = row.Id, status = row.Status.ToString() };
    }

    public async Task<object> ResolvePlanAsync(
        Guid workspaceId,
        int userId,
        Guid id,
        bool approve,
        string? editedScheduleJson,
        CancellationToken cancellationToken)
    {
        var row = await db.AiPlanSuggestions.FirstOrDefaultAsync(
            item => item.Id == id && item.WorkspaceId == workspaceId && item.UserId == userId,
            cancellationToken) ?? throw new InvalidOperationException("برنامه پیدا نشد.");

        if (!string.IsNullOrWhiteSpace(editedScheduleJson)) row.ScheduleJson = editedScheduleJson;
        row.Status = approve ? AiRecommendationStatus.Approved : AiRecommendationStatus.Rejected;
        row.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new { id = row.Id, status = row.Status.ToString(), schedule = JsonSerializer.Deserialize<object>(row.ScheduleJson) };
    }

    public async Task<object> UpdateSettingsAsync(
        Guid workspaceId,
        bool enabled,
        string providerCode,
        string? model,
        bool allowUnderAge,
        int minAgeYears,
        CancellationToken cancellationToken)
    {
        var settings = await context.GetSettingsAsync(workspaceId, cancellationToken);
        settings.Enabled = enabled;
        settings.ProviderCode = string.IsNullOrWhiteSpace(providerCode) ? "heuristic" : providerCode.Trim();
        settings.Model = model?.Trim() ?? "";
        settings.AllowUnderAge = allowUnderAge;
        settings.MinAgeYears = Math.Clamp(minAgeYears, 10, 21);
        settings.UpdatedAt = DateTime.UtcNow;

        var ws = await db.WorkspaceSettings.FirstAsync(item => item.WorkspaceId == workspaceId, cancellationToken);
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(ws.FeatureFlags) ? "{}" : ws.FeatureFlags);
            var map = JsonSerializer.Deserialize<Dictionary<string, object?>>(doc.RootElement.GetRawText()) ?? new();
            map["ai"] = enabled;
            map["aiCoach"] = enabled;
            ws.FeatureFlags = JsonSerializer.Serialize(map);
            ws.UpdatedAt = DateTime.UtcNow;
        }
        catch
        {
            ws.FeatureFlags = $$"""{"ai":{{(enabled ? "true" : "false")}},"aiCoach":{{(enabled ? "true" : "false")}}}""";
            ws.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            enabled = settings.Enabled,
            providerCode = settings.ProviderCode,
            model = settings.Model,
            allowUnderAge = settings.AllowUnderAge,
            minAgeYears = settings.MinAgeYears,
            availableProviders = providers.AvailableCodes,
        };
    }

    public async Task<object> GetSettingsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var settings = await context.GetSettingsAsync(workspaceId, cancellationToken);
        return new
        {
            enabled = settings.Enabled,
            providerCode = settings.ProviderCode,
            model = settings.Model,
            allowUnderAge = settings.AllowUnderAge,
            minAgeYears = settings.MinAgeYears,
            availableProviders = providers.AvailableCodes,
        };
    }

    private async Task<AiWorkspaceSettings> RequireEnabledAsync(Guid workspaceId, int userId, CancellationToken cancellationToken)
    {
        var settings = await context.GetSettingsAsync(workspaceId, cancellationToken);
        if (!settings.Enabled)
            throw new InvalidOperationException("قابلیت هوش مصنوعی برای این فضا غیرفعال است.");

        var userSettings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        JsonElement? data = null;
        if (userSettings is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(userSettings.Data);
                data = doc.RootElement.Clone();
            }
            catch
            {
                // ignore
            }
        }

        if (!AiContextBuilder.PassesAgeGate(settings, data))
            throw new InvalidOperationException("به دلیل محدودیت سنی، هوش مصنوعی برای این حساب فعال نیست.");

        return settings;
    }

    private static List<object> BuildEvidenceRefs(object facts)
    {
        var refs = new List<object>();
        try
        {
            var doc = JsonSerializer.SerializeToElement(facts);
            if (doc.TryGetProperty("lowMasteryTopics", out var mastery) && mastery.ValueKind == JsonValueKind.Array)
            {
                foreach (var node in mastery.EnumerateArray().Take(5))
                {
                    var subject = node.TryGetProperty("Subject", out var s) ? s.GetString()
                        : node.TryGetProperty("subject", out var s2) ? s2.GetString() : null;
                    var topic = node.TryGetProperty("Topic", out var t) ? t.GetString()
                        : node.TryGetProperty("topic", out var t2) ? t2.GetString() : null;
                    var ema = node.TryGetProperty("ScoreEma", out var e) && e.TryGetDouble(out var ev) ? ev
                        : node.TryGetProperty("scoreEma", out var e2) && e2.TryGetDouble(out var ev2) ? ev2 : (double?)null;
                    refs.Add(new
                    {
                        type = "MasteryNode",
                        subject,
                        topic,
                        detail = ema is null ? "تسلط پایین" : $"EMA {ema:0.00}",
                    });
                }
            }

            if (doc.TryGetProperty("weakestSubject", out var weak) && weak.ValueKind == JsonValueKind.String)
            {
                refs.Add(new { type = "Fact", id = "weakestSubject", subject = weak.GetString(), detail = "ضعیف‌ترین درس از facts" });
            }

            if (doc.TryGetProperty("overdueReviews", out var due) && due.TryGetInt32(out var dueCount) && dueCount > 0)
            {
                refs.Add(new { type = "SrsDue", id = "overdueReviews", detail = $"{dueCount} کارت سررسید" });
            }
        }
        catch
        {
            // ignore malformed facts
        }

        return refs;
    }

    private static string HeuristicChatReply(object facts, List<object> evidenceRefs, string confidence)
    {
        var weak = "درس ضعیف";
        try
        {
            var doc = JsonSerializer.SerializeToElement(facts);
            if (doc.TryGetProperty("weakestSubject", out var w) && w.ValueKind == JsonValueKind.String)
                weak = w.GetString() ?? weak;
        }
        catch
        {
            // ignore
        }

        return confidence == "low"
            ? "داده کافی نیست؛ چند ارزیابی یا بازبینی موفق ثبت کن تا دقیق‌تر راهنمایی کنم."
            : $"بر اساس شواهد فعلی، تمرکز کوتاه روی «{weak}» و بازبینی کارت‌های سررسید پیشنهاد می‌شود. ({evidenceRefs.Count} ارجاع شواهد)";
    }

    private static Dictionary<string, object?>? TryParseObject(string text)
    {
        try
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            var json = text[start..(end + 1)];
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }
}
