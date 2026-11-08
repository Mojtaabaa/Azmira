using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

/// <summary>
/// Phase 10.3 F15 — HTTP-level teacher mission confirmation (authz + audit).
/// </summary>
public class TeacherMissionConfirmHttpTests : IClassFixture<HttpPublicApiTests.Factory>
{
    private readonly HttpPublicApiTests.Factory _factory;

    public TeacherMissionConfirmHttpTests(HttpPublicApiTests.Factory factory) => _factory = factory;

    private static string ConfirmPath(Guid questId) => $"/api/teacher/missions/{questId:D}/confirm";

    [Fact]
    public async Task Authorized_teacher_confirms_and_writes_audit()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();
        var response = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.TeacherCookie,
            seed.SchoolWs,
            new { studentUserId = seed.StudentUserId, note = "آفرین" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audits = await db.AuditLogs
            .Where(a => a.Action == "quest.manual_confirm" && a.WorkspaceId == seed.SchoolWs)
            .ToListAsync();
        Assert.Single(audits);
        Assert.Equal(seed.TeacherUserId, audits[0].ActorUserId);
        Assert.Contains("noteSha256", audits[0].DetailJson);
        Assert.Contains(seed.ManualQuestId.ToString("N"), audits[0].TargetId, StringComparison.OrdinalIgnoreCase);

        var progress = await db.UserQuestProgress.SingleAsync(p =>
            p.WorkspaceId == seed.SchoolWs && p.UserId == seed.StudentUserId && p.QuestId == seed.ManualQuestId);
        Assert.Equal(QuestStatus.Completed, progress.Status);
    }

    [Fact]
    public async Task Unauthenticated_confirm_is_rejected()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ConfirmPath(seed.ManualQuestId));
        request.Headers.TryAddWithoutValidation("X-Rotbeh-Workspace", seed.SchoolWs.ToString());
        request.Content = JsonContent.Create(new { studentUserId = seed.StudentUserId });
        var response = await client.SendAsync(request);
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"status={response.StatusCode}");
    }

    [Fact]
    public async Task Student_learner_cannot_confirm()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();
        var response = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.StudentCookie,
            seed.SchoolWs,
            new { studentUserId = seed.StudentUserId });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Foreign_workspace_teacher_cannot_confirm()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();

        // Teacher from another school using foreign workspace header
        var asForeign = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.ForeignTeacherCookie,
            seed.ForeignSchoolWs,
            new { studentUserId = seed.StudentUserId });
        Assert.True(
            asForeign.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest,
            $"foreign ws status={asForeign.StatusCode}");

        // Same cookie but attempting school A's workspace (not a member)
        var crossHeader = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.ForeignTeacherCookie,
            seed.SchoolWs,
            new { studentUserId = seed.StudentUserId });
        Assert.True(
            crossHeader.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest,
            $"cross header status={crossHeader.StatusCode}");
    }

    [Fact]
    public async Task Null_workspace_quest_cannot_be_confirmed()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();
        var response = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.NullWorkspaceQuestId),
            seed.TeacherCookie,
            seed.SchoolWs,
            new { studentUserId = seed.StudentUserId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_identifiers_return_safe_errors()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();

        var missingStudent = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.TeacherCookie,
            seed.SchoolWs,
            new { note = "no student" });
        Assert.Equal(HttpStatusCode.BadRequest, missingStudent.StatusCode);

        var unknownQuest = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(Guid.NewGuid()),
            seed.TeacherCookie,
            seed.SchoolWs,
            new { studentUserId = seed.StudentUserId });
        Assert.Equal(HttpStatusCode.BadRequest, unknownQuest.StatusCode);

        var unknownStudent = await _factory.SendAuthedJsonAsync(
            HttpMethod.Post,
            ConfirmPath(seed.ManualQuestId),
            seed.TeacherCookie,
            seed.SchoolWs,
            new { studentUserId = 9_999_999 });
        Assert.Equal(HttpStatusCode.Forbidden, unknownStudent.StatusCode);
    }

    [Fact]
    public async Task Repeated_confirm_does_not_duplicate_audit_or_notifications()
    {
        var seed = await _factory.SeedTeacherManualMissionAsync();
        var path = ConfirmPath(seed.ManualQuestId);
        var body = new { studentUserId = seed.StudentUserId, note = "ok" };

        var first = await _factory.SendAuthedJsonAsync(HttpMethod.Post, path, seed.TeacherCookie, seed.SchoolWs, body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var second = await _factory.SendAuthedJsonAsync(HttpMethod.Post, path, seed.TeacherCookie, seed.SchoolWs, body);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(a =>
            a.Action == "quest.manual_confirm" && a.WorkspaceId == seed.SchoolWs && a.TargetId == seed.ManualQuestId.ToString("N")));
        Assert.Equal(1, await db.GameNotifications.CountAsync(n =>
            n.WorkspaceId == seed.SchoolWs && n.UserId == seed.StudentUserId && n.Kind == GameNotificationKind.QuestCompleted));
    }
}
