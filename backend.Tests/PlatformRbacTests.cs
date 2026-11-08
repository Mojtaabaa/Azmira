using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Schools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class PlatformRbacTests
{
    private static ServiceProvider BuildSp()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, Guid.NewGuid().ToString("N")));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<SchoolAccess>();
        services.AddScoped<SchoolService>();
        services.AddScoped<SchoolRegistrationService>();
        services.AddScoped<PlatformAdminService>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static int _seq = 5000;

    private static async Task<UserAccount> AddUserAsync(AppDbContext db, string username, byte platformRole = 0)
    {
        var user = new UserAccount
        {
            Id = Interlocked.Increment(ref _seq),
            Username = username,
            NormalizedUsername = username.ToUpperInvariant(),
            PasswordHash = "x",
            DisplayName = username,
            PlatformRole = platformRole,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task User_can_request_school_and_admin_approves()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registrations = scope.ServiceProvider.GetRequiredService<SchoolRegistrationService>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformAdminService>();

        var user = await AddUserAsync(db, "requester");
        var admin = await AddUserAsync(db, "padmin", PlatformRoles.Administrator);

        await registrations.CreateRequestAsync(user.Id, "مدرسه نوین", "تهران", null, CancellationToken.None);
        var pending = await registrations.ListPendingAsync(CancellationToken.None);
        var pendingJson = JsonDocument.Parse(JsonSerializer.Serialize(pending));
        var requestId = pendingJson.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();

        await registrations.ApproveAsync(requestId, admin.Id, CancellationToken.None);

        Assert.Equal(1, await db.Schools.CountAsync());
        Assert.Equal(SchoolRegistrationRequestStatus.Approved, (await db.SchoolRegistrationRequests.SingleAsync()).Status);
        var member = await db.WorkspaceMembers.SingleAsync(item => item.UserId == user.Id && item.Workspace.Type == WorkspaceType.School);
        Assert.Equal(WorkspaceMemberRole.SchoolOwner, member.Role);

        var dash = JsonDocument.Parse(JsonSerializer.Serialize(await platform.DashboardAsync(CancellationToken.None)));
        Assert.Equal(0, dash.RootElement.GetProperty("pendingSchoolRequests").GetInt32());
    }

    [Fact]
    public async Task Cannot_remove_last_platform_admin()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformAdminService>();
        var admin = await AddUserAsync(db, "onlyadmin", PlatformRoles.Administrator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            platform.SetPlatformRoleAsync(admin.Id, admin.Id, 0, CancellationToken.None));
    }

    [Fact]
    public async Task Cannot_demote_last_school_owner()
    {
        await using var sp = BuildSp();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var schools = scope.ServiceProvider.GetRequiredService<SchoolService>();
        var owner = await AddUserAsync(db, "owner1");
        var created = await schools.RegisterAsync(owner.Id, "مدرسه تست", null, null, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            schools.InviteMemberAsync(created.WorkspaceId, owner.Id, owner.Id, WorkspaceMemberRole.Teacher, CancellationToken.None));
    }

    [Fact]
    public void Capabilities_hide_teacher_for_regular_personal_user()
    {
        var list = new List<WorkspaceListItem>
        {
            new(Guid.NewGuid(), WorkspaceType.Personal, "شخصی", WorkspaceMemberRole.Student),
        };
        var caps = UserCapabilities.Build(0, list, list[0].Id);
        var json = JsonSerializer.Serialize(caps);
        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("teacher").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("parent").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("canRequestSchool").GetBoolean());
    }
}
