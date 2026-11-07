using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class SchoolEditionTests
{
    private static ServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<SchoolAccess>();
        services.AddScoped<SchoolService>();
        services.AddScoped<TeacherPortalService>();
        services.AddScoped<ParentPortalService>();
        services.AddScoped<SchoolAnalyticsService>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static int _nextUserId = 1000;

    private static async Task<(ServiceProvider Sp, int OwnerId)> SeedOwnerAsync()
    {
        var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = new UserAccount
        {
            Id = Interlocked.Increment(ref _nextUserId),
            Username = "owner",
            NormalizedUsername = "owner",
            PasswordHash = "x",
            DisplayName = "مدیر",
            CreatedAt = DateTime.UtcNow,
            PlatformRole = 0,
        };
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        return (sp, owner.Id);
    }

    private static async Task<int> AddUserAsync(AppDbContext db, string username)
    {
        var user = new UserAccount
        {
            Id = Interlocked.Increment(ref _nextUserId),
            Username = username,
            NormalizedUsername = username,
            PasswordHash = "x",
            DisplayName = username,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task School_registration_creates_workspace_without_touching_personal()
    {
        var (sp, ownerId) = await SeedOwnerAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();

        // personal workspace already may not exist; create one manually to prove isolation
        var personalId = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = personalId,
            Type = WorkspaceType.Personal,
            Name = "فضای شخصی",
            OwnerUserId = ownerId,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(ownerId, "دبیرستان الف", "تهران", null, CancellationToken.None)));
        var schoolWs = created.RootElement.GetProperty("workspaceId").GetGuid();

        Assert.Equal(2, await db.Workspaces.CountAsync());
        Assert.True(await db.Workspaces.AnyAsync(item => item.Id == personalId && item.Type == WorkspaceType.Personal));
        Assert.True(await db.Workspaces.AnyAsync(item => item.Id == schoolWs && item.Type == WorkspaceType.School));
        Assert.Equal(WorkspaceMemberRole.SchoolOwner, (await db.WorkspaceMembers.SingleAsync(item => item.WorkspaceId == schoolWs)).Role);
    }

    [Fact]
    public async Task Teacher_cannot_access_student_outside_assigned_class()
    {
        var (sp, ownerId) = await SeedOwnerAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();
        var teacherPortal = scope.ServiceProvider.GetRequiredService<TeacherPortalService>();
        var access = scope.ServiceProvider.GetRequiredService<SchoolAccess>();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(ownerId, "مدرسه ب", null, null, CancellationToken.None)));
        var workspaceId = created.RootElement.GetProperty("workspaceId").GetGuid();
        var teacherId = await AddUserAsync(db, "teacher1");
        var studentIn = await AddUserAsync(db, "stu1");
        var studentOut = await AddUserAsync(db, "stu2");
        await schools.InviteMemberAsync(workspaceId, ownerId, teacherId, WorkspaceMemberRole.Teacher, CancellationToken.None);
        await schools.InviteMemberAsync(workspaceId, ownerId, studentIn, WorkspaceMemberRole.Student, CancellationToken.None);
        await schools.InviteMemberAsync(workspaceId, ownerId, studentOut, WorkspaceMemberRole.Student, CancellationToken.None);

        var classA = JsonDocument.Parse(JsonSerializer.Serialize(await schools.CreateClassAsync(workspaceId, teacherId, "۱۰۱", null, null, CancellationToken.None)));
        var classB = JsonDocument.Parse(JsonSerializer.Serialize(await schools.CreateClassAsync(workspaceId, ownerId, "۱۰۲", null, null, CancellationToken.None)));
        var classAId = classA.RootElement.GetProperty("id").GetGuid();
        var classBId = classB.RootElement.GetProperty("id").GetGuid();

        db.ClassMembers.Add(new ClassMember { Id = Guid.NewGuid(), ClassId = classAId, UserId = studentIn, Role = ClassMemberRole.Student, JoinedAt = DateTime.UtcNow });
        db.ClassMembers.Add(new ClassMember { Id = Guid.NewGuid(), ClassId = classBId, UserId = studentOut, Role = ClassMemberRole.Student, JoinedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await teacherPortal.StudentProgressAsync(workspaceId, teacherId, WorkspaceMemberRole.Teacher, studentIn, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await teacherPortal.StudentProgressAsync(workspaceId, teacherId, WorkspaceMemberRole.Teacher, studentOut, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherId, classBId, WorkspaceMemberRole.Teacher, CancellationToken.None));
    }

    [Fact]
    public async Task Parent_only_sees_verified_children_and_cannot_mutate_via_summary()
    {
        var (sp, ownerId) = await SeedOwnerAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();
        var parents = scope.ServiceProvider.GetRequiredService<ParentPortalService>();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(ownerId, "مدرسه ج", null, null, CancellationToken.None)));
        var workspaceId = created.RootElement.GetProperty("workspaceId").GetGuid();
        var parentId = await AddUserAsync(db, "parent1");
        var childId = await AddUserAsync(db, "child1");
        var otherChild = await AddUserAsync(db, "child2");
        await schools.InviteMemberAsync(workspaceId, ownerId, parentId, WorkspaceMemberRole.Parent, CancellationToken.None);
        await schools.InviteMemberAsync(workspaceId, ownerId, childId, WorkspaceMemberRole.Student, CancellationToken.None);
        await schools.InviteMemberAsync(workspaceId, ownerId, otherChild, WorkspaceMemberRole.Student, CancellationToken.None);

        var link = JsonDocument.Parse(JsonSerializer.Serialize(await parents.RequestLinkAsync(workspaceId, parentId, childId, CancellationToken.None)));
        Assert.Equal("Pending", link.RootElement.GetProperty("status").GetString());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            parents.ChildSummaryAsync(workspaceId, parentId, childId, CancellationToken.None));

        await parents.VerifyLinkAsync(workspaceId, ownerId, link.RootElement.GetProperty("id").GetGuid(), true, CancellationToken.None);
        var summary = JsonDocument.Parse(JsonSerializer.Serialize(await parents.ChildSummaryAsync(workspaceId, parentId, childId, CancellationToken.None)));
        Assert.True(summary.RootElement.GetProperty("readOnly").GetBoolean());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            parents.ChildSummaryAsync(workspaceId, parentId, otherChild, CancellationToken.None));
    }

    [Fact]
    public async Task School_isolation_blocks_cross_tenant_class_access()
    {
        var (sp, ownerId) = await SeedOwnerAsync();
        await using var scope = sp.CreateAsyncScope();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();
        var access = scope.ServiceProvider.GetRequiredService<SchoolAccess>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var a = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(ownerId, "مدرسه ۱", null, null, CancellationToken.None)));
        var owner2 = await AddUserAsync(db, "owner2");
        var b = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(owner2, "مدرسه ۲", null, null, CancellationToken.None)));
        var wsA = a.RootElement.GetProperty("workspaceId").GetGuid();
        var wsB = b.RootElement.GetProperty("workspaceId").GetGuid();
        var classB = JsonDocument.Parse(JsonSerializer.Serialize(await schools.CreateClassAsync(wsB, owner2, "کلاس ب", null, null, CancellationToken.None)));
        var classBId = classB.RootElement.GetProperty("id").GetGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            access.EnsureTeacherOwnsClassAsync(wsA, ownerId, classBId, WorkspaceMemberRole.SchoolOwner, CancellationToken.None));
    }

    [Fact]
    public async Task Analytics_teacher_scoped_to_own_classes()
    {
        var (sp, ownerId) = await SeedOwnerAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();
        var analytics = scope.ServiceProvider.GetRequiredService<SchoolAnalyticsService>();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await schools.RegisterAsync(ownerId, "مدرسه د", null, null, CancellationToken.None)));
        var workspaceId = created.RootElement.GetProperty("workspaceId").GetGuid();
        var teacherId = await AddUserAsync(db, "t-analytics");
        await schools.InviteMemberAsync(workspaceId, ownerId, teacherId, WorkspaceMemberRole.Teacher, CancellationToken.None);
        var classJson = JsonDocument.Parse(JsonSerializer.Serialize(await schools.CreateClassAsync(workspaceId, teacherId, "آمار", null, null, CancellationToken.None)));
        var classId = classJson.RootElement.GetProperty("id").GetGuid();

        var payload = JsonDocument.Parse(JsonSerializer.Serialize(await analytics.DashboardAsync(
            workspaceId, WorkspaceMemberRole.Teacher, teacherId, null, null, classId, null, null, null, CancellationToken.None)));
        Assert.True(payload.RootElement.TryGetProperty("studentActivity", out _));

        var otherClass = JsonDocument.Parse(JsonSerializer.Serialize(await schools.CreateClassAsync(workspaceId, ownerId, "دیگر", null, null, CancellationToken.None)));
        var otherClassId = otherClass.RootElement.GetProperty("id").GetGuid();
        var access = scope.ServiceProvider.GetRequiredService<SchoolAccess>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await access.EnsureTeacherOwnsClassAsync(workspaceId, teacherId, otherClassId, WorkspaceMemberRole.Teacher, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await analytics.DashboardAsync(
                workspaceId, WorkspaceMemberRole.Teacher, teacherId, null, null,
                otherClassId, null, null, null, CancellationToken.None));
    }
}
