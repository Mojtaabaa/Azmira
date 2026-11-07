using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class LearningIntelligenceEndpoints
{
    public static RouteGroupBuilder MapLearningIntelligence(this RouteGroupBuilder group)
    {
        MapAssessments(group);
        MapMastery(group);
        MapReviews(group);
        MapAi(group);
        return group;
    }

    private static void MapAssessments(RouteGroupBuilder group)
    {
        group.MapGet("/assessments", async (HttpContext http, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await assessments.ListAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapGet("/assessments/{id:guid}", async (HttpContext http, Guid id, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await assessments.GetAsync(ws.WorkspaceId, id, includeAnswers: false, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        group.MapPost("/assessments", async (HttpContext http, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null) return Results.Json(new { error = "بدنه نامعتبر است." }, statusCode: 400);

            var title = ReadString(body, "title") ?? "";
            var description = ReadString(body, "description");
            var kind = Enum.TryParse<AssessmentKind>(ReadString(body, "kind") ?? "SelfAssessment", true, out var k) ? k : AssessmentKind.SelfAssessment;
            var subject = ReadString(body, "subject") ?? "";
            var chapter = ReadString(body, "chapter");
            var topic = ReadString(body, "topic");
            Guid? classId = null;
            if (body.RootElement.TryGetProperty("classId", out var c) && c.TryGetGuid(out var cid)) classId = cid;
            var passScore = body.RootElement.TryGetProperty("passScore", out var p) && p.TryGetDouble(out var pv) ? pv : 70;
            int? timeLimit = body.RootElement.TryGetProperty("timeLimitMinutes", out var t) && t.TryGetInt32(out var tv) ? tv : null;
            var publish = body.RootElement.TryGetProperty("publish", out var pub) && pub.ValueKind == JsonValueKind.True;
            var questions = ParseQuestions(body);

            try
            {
                return Results.Json(await assessments.CreateAsync(
                    ws.WorkspaceId, ws.UserId, title, description, kind, subject, chapter, topic, classId, passScore, timeLimit, questions, publish, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/assessments/{id:guid}/attempts", async (HttpContext http, Guid id, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await assessments.StartAttemptAsync(ws.WorkspaceId, ws.UserId, id, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/assessments/attempts/{attemptId:guid}/submit", async (HttpContext http, Guid attemptId, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var answers = ParseAnswers(body);
            try
            {
                return Results.Json(await assessments.SubmitAttemptAsync(ws.WorkspaceId, ws.UserId, attemptId, answers, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/assessments/attempts/me", async (HttpContext http, AssessmentService assessments, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await assessments.AttemptHistoryAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });
    }

    private static void MapMastery(RouteGroupBuilder group)
    {
        group.MapGet("/mastery", async (HttpContext http, MasteryService mastery, string? subject, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await mastery.GetMapAsync(ws.WorkspaceId, ws.UserId, subject, cancellationToken));
        });
    }

    private static void MapReviews(RouteGroupBuilder group)
    {
        group.MapGet("/reviews/due", async (HttpContext http, SpacedRepetitionService srs, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await srs.DueAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapGet("/reviews/history", async (HttpContext http, SpacedRepetitionService srs, Guid? cardId, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await srs.HistoryAsync(ws.WorkspaceId, ws.UserId, cardId, cancellationToken));
        });

        group.MapPost("/reviews/{cardId:guid}", async (HttpContext http, Guid cardId, SpacedRepetitionService srs, MasteryService mastery, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var outcome = Enum.TryParse<ReviewOutcome>(ReadString(body, "outcome") ?? "Good", true, out var o) ? o : ReviewOutcome.Good;
            byte difficulty = 3;
            byte confidence = 3;
            if (body?.RootElement.TryGetProperty("difficulty", out var d) == true && d.TryGetByte(out var dv)) difficulty = dv;
            if (body?.RootElement.TryGetProperty("confidence", out var c) == true && c.TryGetByte(out var cv)) confidence = cv;
            try
            {
                return Results.Json(await srs.ReviewAsync(ws.WorkspaceId, ws.UserId, cardId, outcome, difficulty, confidence, mastery, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/reviews/cards", async (HttpContext http, SpacedRepetitionService srs, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var subject = ReadString(body, "subject") ?? "";
            var chapter = ReadString(body, "chapter");
            var topic = ReadString(body, "topic") ?? "";
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(topic))
                return Results.Json(new { error = "subject و topic لازم است." }, statusCode: 400);
            return Results.Json(await srs.EnsureCardAsync(ws.WorkspaceId, ws.UserId, subject, chapter, topic, cancellationToken));
        });
    }

    private static void MapAi(RouteGroupBuilder group)
    {
        group.MapGet("/ai/settings", async (HttpContext http, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await coach.GetSettingsAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapPut("/ai/settings", async (HttpContext http, AppDbContext db, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (await RequireAiAdmin(http, db, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var enabled = body?.RootElement.TryGetProperty("enabled", out var e) == true && e.ValueKind == JsonValueKind.True;
            var provider = ReadString(body, "providerCode") ?? "heuristic";
            var model = ReadString(body, "model");
            var allowUnderAge = body?.RootElement.TryGetProperty("allowUnderAge", out var a) == true && a.ValueKind == JsonValueKind.True;
            var minAge = body?.RootElement.TryGetProperty("minAgeYears", out var m) == true && m.TryGetInt32(out var mv) ? mv : 13;
            return Results.Json(await coach.UpdateSettingsAsync(ws.WorkspaceId, enabled, provider, model, allowUnderAge, minAge, cancellationToken));
        });

        group.MapPost("/ai/coach/analyze", async (HttpContext http, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            int? target = null;
            if (body?.RootElement.TryGetProperty("userId", out var u) == true && u.TryGetInt32(out var uid)) target = uid;
            try
            {
                return Results.Json(await coach.AnalyzeAsync(ws.WorkspaceId, ws.UserId, target, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/ai/report/weekly", async (HttpContext http, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            int? target = null;
            if (body?.RootElement.TryGetProperty("userId", out var u) == true && u.TryGetInt32(out var uid)) target = uid;
            try
            {
                return Results.Json(await coach.WeeklyReportAsync(ws.WorkspaceId, ws.UserId, target, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/ai/planner/suggest", async (HttpContext http, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var minutes = body?.RootElement.TryGetProperty("availableMinutes", out var m) == true && m.TryGetInt32(out var mv) ? mv : 120;
            var commitments = body?.RootElement.TryGetProperty("commitments", out var c) == true ? c.GetRawText() : null;
            try
            {
                return Results.Json(await coach.SuggestPlanAsync(ws.WorkspaceId, ws.UserId, minutes, commitments, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/ai/recommendations/{id:guid}/resolve", async (HttpContext http, Guid id, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var approve = body?.RootElement.TryGetProperty("approve", out var a) != true || a.ValueKind != JsonValueKind.False;
            var edited = body?.RootElement.TryGetProperty("payload", out var p) == true ? p.GetRawText() : null;
            try
            {
                return Results.Json(await coach.ResolveRecommendationAsync(ws.WorkspaceId, ws.UserId, id, approve, edited, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/ai/planner/{id:guid}/resolve", async (HttpContext http, Guid id, AiCoachService coach, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var approve = body?.RootElement.TryGetProperty("approve", out var a) != true || a.ValueKind != JsonValueKind.False;
            var edited = body?.RootElement.TryGetProperty("schedule", out var s) == true ? s.GetRawText() : null;
            try
            {
                return Results.Json(await coach.ResolvePlanAsync(ws.WorkspaceId, ws.UserId, id, approve, edited, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });
    }

    private static List<QuestionInput> ParseQuestions(JsonDocument body)
    {
        var list = new List<QuestionInput>();
        if (!body.RootElement.TryGetProperty("questions", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in arr.EnumerateArray())
        {
            var type = Enum.TryParse<QuestionType>(item.TryGetProperty("type", out var t) ? t.GetString() : "MultipleChoice", true, out var qt)
                ? qt
                : QuestionType.MultipleChoice;
            var prompt = item.TryGetProperty("prompt", out var p) ? p.GetString() ?? "" : "";
            var options = item.TryGetProperty("options", out var o) ? o.GetRawText() : "[]";
            var correct = item.TryGetProperty("correctAnswer", out var c) ? c.GetRawText() : "null";
            var points = item.TryGetProperty("points", out var pts) && pts.TryGetDouble(out var pv) ? pv : 1;
            var subject = item.TryGetProperty("subject", out var s) ? s.GetString() : null;
            var chapter = item.TryGetProperty("chapter", out var ch) ? ch.GetString() : null;
            var topic = item.TryGetProperty("topic", out var tp) ? tp.GetString() : null;
            list.Add(new QuestionInput(type, prompt, options, correct, points, subject, chapter, topic));
        }

        return list;
    }

    private static List<AnswerInput> ParseAnswers(JsonDocument? body)
    {
        var list = new List<AnswerInput>();
        if (body is null || !body.RootElement.TryGetProperty("answers", out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetProperty("questionId", out var q) || !q.TryGetGuid(out var qid)) continue;
            var response = item.TryGetProperty("response", out var r) ? r.GetRawText() : "null";
            int? duration = item.TryGetProperty("durationSeconds", out var d) && d.TryGetInt32(out var dv) ? dv : null;
            list.Add(new AnswerInput(qid, response, duration));
        }

        return list;
    }

    private static string? ReadString(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static async Task<IResult?> RequireAiAdmin(HttpContext http, AppDbContext db, CancellationToken cancellationToken)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return null;
        if (ws.Role is WorkspaceMemberRole.SchoolOwner or WorkspaceMemberRole.SchoolAdministrator) return null;
        var workspace = await db.Workspaces.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.WorkspaceId, cancellationToken);
        if (workspace?.Type == WorkspaceType.Personal && workspace.OwnerUserId == ws.UserId) return null;
        return Results.Json(new { error = "فقط مدیر می‌تواند تنظیمات هوش مصنوعی را تغییر دهد." }, statusCode: 403);
    }
}
