using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Api;

public static class SchoolEndpoints
{
    public static RouteGroupBuilder MapSchoolEdition(this RouteGroupBuilder group)
    {
        group.MapPost("/schools", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (WorkspaceAccess.RequireUser(http) is { } denied) return denied;
            var user = WorkspaceAccess.User(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var name = ReadString(body, "name") ?? "";
            var city = ReadString(body, "city");
            var description = ReadString(body, "description");
            try
            {
                return Results.Json(await schools.RegisterAsync(user.Id, name, city, description, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/schools/me", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await schools.GetProfileAsync(ws.WorkspaceId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 404);
            }
        });

        group.MapPut("/schools/me", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            try
            {
                return Results.Json(await schools.UpdateProfileAsync(
                    ws.WorkspaceId,
                    ReadString(body, "name"),
                    ReadString(body, "description"),
                    ReadString(body, "city"),
                    ReadString(body, "address"),
                    ReadString(body, "phone"),
                    ReadBool(body, "leaderboardsVisible"),
                    ReadBool(body, "allowTeacherBadges"),
                    cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/schools/members", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await schools.ListMembersAsync(ws.WorkspaceId, cancellationToken));
        });

        group.MapPost("/schools/members", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("userId", out var u) || !u.TryGetInt32(out var userId))
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            var role = Enum.TryParse<WorkspaceMemberRole>(ReadString(body, "role") ?? "Student", true, out var r) ? r : WorkspaceMemberRole.Student;
            try
            {
                return Results.Json(await schools.InviteMemberAsync(ws.WorkspaceId, ws.UserId, userId, role, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        MapStructure(group);
        MapTeacher(group);
        MapParent(group);
        MapAnalytics(group);
        return group;
    }

    private static void MapStructure(RouteGroupBuilder group)
    {
        group.MapGet("/schools/years", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            return Results.Json(await schools.ListAcademicYearsAsync(WorkspaceAccess.Workspace(http).WorkspaceId, cancellationToken));
        });

        group.MapPost("/schools/years", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var name = ReadString(body, "name") ?? "";
            var starts = ReadDateOnly(body, "startsOn") ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var ends = ReadDateOnly(body, "endsOn") ?? starts.AddMonths(10);
            var isCurrent = ReadBool(body, "isCurrent") ?? true;
            try
            {
                return Results.Json(await schools.CreateAcademicYearAsync(ws.WorkspaceId, name, starts, ends, isCurrent, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/schools/grades", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            return Results.Json(await schools.ListGradesAsync(WorkspaceAccess.Workspace(http).WorkspaceId, cancellationToken));
        });

        group.MapPost("/schools/grades", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            try
            {
                return Results.Json(await schools.CreateGradeAsync(
                    WorkspaceAccess.Workspace(http).WorkspaceId,
                    ReadString(body, "name") ?? "",
                    ReadInt(body, "sortOrder") ?? 0,
                    cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/schools/subjects", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            return Results.Json(await schools.ListSubjectsAsync(WorkspaceAccess.Workspace(http).WorkspaceId, cancellationToken));
        });

        group.MapPost("/schools/subjects", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            try
            {
                return Results.Json(await schools.CreateSubjectAsync(
                    WorkspaceAccess.Workspace(http).WorkspaceId,
                    ReadString(body, "name") ?? "",
                    ReadString(body, "code"),
                    cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/schools/classes", async (HttpContext http, SchoolService schools, Guid? academicYearId, Guid? gradeId, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            return Results.Json(await schools.ListClassesAsync(WorkspaceAccess.Workspace(http).WorkspaceId, academicYearId, gradeId, cancellationToken));
        });

        group.MapPost("/schools/classes", async (HttpContext http, SchoolService schools, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            Guid? yearId = null;
            Guid? gradeId = null;
            if (body?.RootElement.TryGetProperty("academicYearId", out var y) == true && y.TryGetGuid(out var yid)) yearId = yid;
            if (body?.RootElement.TryGetProperty("gradeId", out var g) == true && g.TryGetGuid(out var gid)) gradeId = gid;
            try
            {
                return Results.Json(await schools.CreateClassAsync(ws.WorkspaceId, ws.UserId, ReadString(body, "name") ?? "", yearId, gradeId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/schools/classes/{id:guid}/members", async (HttpContext http, Guid id, AppDbContext db, SchoolAccess access, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("userId", out var u) || !u.TryGetInt32(out var userId))
                return Results.Json(new { error = "userId لازم است." }, statusCode: 400);
            var roleText = ReadString(body, "role") ?? "Student";
            var role = Enum.TryParse<ClassMemberRole>(roleText, true, out var parsed) ? parsed : ClassMemberRole.Student;
            try
            {
                await access.EnsureTeacherOwnsClassAsync(ws.WorkspaceId, ws.UserId, id, ws.Role, cancellationToken);
                if (!await db.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == ws.WorkspaceId && item.UserId == userId && item.Status == WorkspaceMemberStatus.Active, cancellationToken))
                    return Results.Json(new { error = "کاربر عضو مدرسه نیست." }, statusCode: 400);
                if (await db.ClassMembers.AnyAsync(item => item.ClassId == id && item.UserId == userId, cancellationToken))
                    return Results.Json(new { ok = true, already = true });
                db.ClassMembers.Add(new ClassMember
                {
                    Id = Guid.NewGuid(),
                    ClassId = id,
                    UserId = userId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
                return Results.Json(new { ok = true });
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });
    }

    private static void MapTeacher(RouteGroupBuilder group)
    {
        group.MapGet("/teacher/classes", async (HttpContext http, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await teacher.MyClassesAsync(ws.WorkspaceId, ws.UserId, ws.Role, cancellationToken));
        });

        group.MapGet("/teacher/classes/{id:guid}/students", async (HttpContext http, Guid id, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await teacher.ClassStudentsAsync(ws.WorkspaceId, ws.UserId, ws.Role, id, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        group.MapGet("/teacher/students/{studentUserId:int}/progress", async (HttpContext http, int studentUserId, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await teacher.StudentProgressAsync(ws.WorkspaceId, ws.UserId, ws.Role, studentUserId, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        group.MapGet("/teacher/assignments", async (HttpContext http, TeacherPortalService teacher, Guid? classId, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await teacher.ListAssignmentsAsync(ws.WorkspaceId, ws.UserId, ws.Role, classId, cancellationToken));
        });

        group.MapPost("/teacher/assignments", async (HttpContext http, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("classId", out var c) || !c.TryGetGuid(out var classId))
                return Results.Json(new { error = "classId لازم است." }, statusCode: 400);
            Guid? subjectId = null;
            if (body.RootElement.TryGetProperty("subjectId", out var s) && s.TryGetGuid(out var sid)) subjectId = sid;
            DateTime? dueAt = null;
            if (body.RootElement.TryGetProperty("dueAt", out var d) && d.ValueKind == JsonValueKind.String && DateTime.TryParse(d.GetString(), out var parsed))
                dueAt = parsed.ToUniversalTime();
            try
            {
                return Results.Json(await teacher.CreateAssignmentAsync(
                    ws.WorkspaceId, ws.UserId, ws.Role, classId, subjectId,
                    ReadString(body, "title") ?? "", ReadString(body, "description"), dueAt,
                    ReadBool(body, "publish") ?? true, cancellationToken));
            }
            catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = error.Message }, statusCode: error is UnauthorizedAccessException ? 403 : 400);
            }
        });

        group.MapPost("/teacher/missions", async (HttpContext http, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("classId", out var c) || !c.TryGetGuid(out var classId))
                return Results.Json(new { error = "classId لازم است." }, statusCode: 400);
            int? assigned = null;
            if (body.RootElement.TryGetProperty("assignedUserId", out var a) && a.TryGetInt32(out var aid)) assigned = aid;
            try
            {
                return Results.Json(await teacher.CreateMissionAsync(
                    ws.WorkspaceId, ws.UserId, ws.Role, classId,
                    ReadString(body, "title") ?? "", ReadString(body, "description"),
                    ReadInt(body, "targetValue") ?? 1, ReadInt(body, "rewardXp") ?? 20, ReadInt(body, "rewardCoins") ?? 5,
                    assigned, cancellationToken));
            }
            catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = error.Message }, statusCode: error is UnauthorizedAccessException ? 403 : 400);
            }
        });

        group.MapGet("/teacher/classes/{id:guid}/activity", async (HttpContext http, Guid id, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await teacher.MonitorActivityAsync(ws.WorkspaceId, ws.UserId, ws.Role, id, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });

        group.MapPost("/teacher/feedback", async (HttpContext http, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("studentUserId", out var s) || !s.TryGetInt32(out var studentId))
                return Results.Json(new { error = "studentUserId لازم است." }, statusCode: 400);
            Guid? classId = null;
            if (body.RootElement.TryGetProperty("classId", out var c) && c.TryGetGuid(out var cid)) classId = cid;
            try
            {
                return Results.Json(await teacher.AddFeedbackAsync(ws.WorkspaceId, ws.UserId, ws.Role, studentId, classId, ReadString(body, "body") ?? "", cancellationToken));
            }
            catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = error.Message }, statusCode: error is UnauthorizedAccessException ? 403 : 400);
            }
        });

        group.MapPost("/teacher/badges/award", async (HttpContext http, TeacherPortalService teacher, CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null
                || !body.RootElement.TryGetProperty("studentUserId", out var s) || !s.TryGetInt32(out var studentId)
                || !body.RootElement.TryGetProperty("badgeId", out var b) || !b.TryGetGuid(out var badgeId))
                return Results.Json(new { error = "studentUserId و badgeId لازم است." }, statusCode: 400);
            try
            {
                return Results.Json(await teacher.AwardBadgeAsync(ws.WorkspaceId, ws.UserId, ws.Role, studentId, badgeId, cancellationToken));
            }
            catch (Exception error) when (error is InvalidOperationException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = error.Message }, statusCode: error is UnauthorizedAccessException ? 403 : 400);
            }
        });
    }

    private static void MapParent(RouteGroupBuilder group)
    {
        group.MapPost("/parent/links", async (HttpContext http, ParentPortalService parents, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            if (body is null || !body.RootElement.TryGetProperty("studentUserId", out var s) || !s.TryGetInt32(out var studentId))
                return Results.Json(new { error = "studentUserId لازم است." }, statusCode: 400);
            try
            {
                return Results.Json(await parents.RequestLinkAsync(ws.WorkspaceId, ws.UserId, studentId, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapPost("/parent/links/{id:guid}/verify", async (HttpContext http, Guid id, ParentPortalService parents, CancellationToken cancellationToken) =>
        {
            if (await RequireAdmin(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            var body = await JsonDocumentSafe.Read(http, cancellationToken);
            var approve = ReadBool(body, "approve") ?? true;
            try
            {
                return Results.Json(await parents.VerifyLinkAsync(ws.WorkspaceId, ws.UserId, id, approve, cancellationToken));
            }
            catch (InvalidOperationException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 400);
            }
        });

        group.MapGet("/parent/children", async (HttpContext http, ParentPortalService parents, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            return Results.Json(await parents.ListChildrenAsync(ws.WorkspaceId, ws.UserId, cancellationToken));
        });

        group.MapGet("/parent/children/{studentUserId:int}/summary", async (HttpContext http, int studentUserId, ParentPortalService parents, CancellationToken cancellationToken) =>
        {
            if (RequireSchool(http) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await parents.ChildSummaryAsync(ws.WorkspaceId, ws.UserId, studentUserId, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });
    }

    private static void MapAnalytics(RouteGroupBuilder group)
    {
        group.MapGet("/schools/analytics", async (
            HttpContext http,
            SchoolAnalyticsService analytics,
            Guid? academicYearId,
            Guid? gradeId,
            Guid? classId,
            string? subject,
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken) =>
        {
            if (await RequireTeacher(http, cancellationToken) is { } denied) return denied;
            var ws = WorkspaceAccess.Workspace(http);
            try
            {
                return Results.Json(await analytics.DashboardAsync(
                    ws.WorkspaceId, ws.Role, ws.UserId, academicYearId, gradeId, classId, subject, from, to, cancellationToken));
            }
            catch (UnauthorizedAccessException error)
            {
                return Results.Json(new { error = error.Message }, statusCode: 403);
            }
        });
    }

    private static IResult? RequireSchool(HttpContext http)
    {
        if (WorkspaceAccess.RequireWorkspace(http) is { } denied) return denied;
        if (WorkspaceAccess.Workspace(http).Type != WorkspaceType.School)
            return Results.Json(new { error = "فقط در فضای مدرسه." }, statusCode: 400);
        return null;
    }

    private static Task<IResult?> RequireAdmin(HttpContext http, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (RequireSchool(http) is { } denied) return Task.FromResult<IResult?>(denied);
        var ws = WorkspaceAccess.Workspace(http);
        if (SchoolAccess.IsAdmin(ws.Role) || ws.Role == WorkspaceMemberRole.AcademicManager)
            return Task.FromResult<IResult?>(null);
        // Platform admin via user flag checked loosely: allow SchoolOwner/Admin already
        return Task.FromResult<IResult?>(Results.Json(new { error = "دسترسی مدیریتی مدرسه لازم است." }, statusCode: 403));
    }

    private static async Task<IResult?> RequireTeacher(HttpContext http, CancellationToken cancellationToken)
    {
        if (RequireSchool(http) is { } denied) return denied;
        var ws = WorkspaceAccess.Workspace(http);
        if (SchoolAccess.IsTeacherOrAbove(ws.Role)) return null;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == ws.UserId, cancellationToken);
        if (user?.PlatformRole == 100) return null;
        return Results.Json(new { error = "دسترسی معلم/مدیر لازم است." }, statusCode: 403);
    }

    private static string? ReadString(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static int? ReadInt(JsonDocument? body, string name) =>
        body is not null && body.RootElement.TryGetProperty(name, out var el) && el.TryGetInt32(out var v) ? v : null;

    private static bool? ReadBool(JsonDocument? body, string name)
    {
        if (body is null || !body.RootElement.TryGetProperty(name, out var el)) return null;
        if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
        return null;
    }

    private static DateOnly? ReadDateOnly(JsonDocument? body, string name)
    {
        if (body is null || !body.RootElement.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String) return null;
        return DateOnly.TryParse(el.GetString(), out var d) ? d : null;
    }

}
