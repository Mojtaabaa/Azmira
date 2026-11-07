using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class SocialGamificationTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateAsync(WorkspaceType type = WorkspaceType.Personal)
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<XpEngine>();
        services.AddScoped<LevelEngine>();
        services.AddScoped<BadgeEngine>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<StreakEngine>();
        services.AddScoped<GamificationQueryService>();
        services.AddScoped<GamificationPipeline>();
        services.AddScoped<LeaderboardService>();
        services.AddScoped<TeamService>();
        services.AddScoped<CompetitionService>();
        services.AddScoped<StoreService>();
        services.AddScoped<RewardService>();
        services.AddScoped<ClassService>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new KonkoorHelp.Api.Services.Integrations.IntegrationsOptions
        {
            WebhookProtectionKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        }));
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookSecretProtector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookDnsResolver, KonkoorHelp.Api.Services.Integrations.SystemWebhookDnsResolver>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector, KonkoorHelp.Api.Services.Integrations.TcpWebhookSocketConnector>();
        services.AddSingleton<KonkoorHelp.Api.Services.Integrations.WebhookUrlSafety>();
        services.AddScoped<KonkoorHelp.Api.Services.Integrations.WebhookService>();
        services.AddHttpClient(nameof(KonkoorHelp.Api.Services.Integrations.WebhookService), client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }).ConfigurePrimaryHttpMessageHandler(sp =>
            KonkoorHelp.Api.Services.Integrations.WebhookSocketsHttpHandlerFactory.Create(
                sp.GetRequiredService<KonkoorHelp.Api.Services.Integrations.IWebhookSocketConnector>()));
        services.AddLogging();

        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "social",
            NormalizedUsername = "social",
            PasswordHash = "x",
            DisplayName = "بازیکن اجتماعی",
            CreatedAt = DateTime.UtcNow,
            PlatformRole = 100,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var workspaceId = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Type = type,
            Name = type == WorkspaceType.School ? "مدرسه تست" : "فضای شخصی",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = type == WorkspaceType.School ? WorkspaceMemberRole.SchoolOwner : WorkspaceMemberRole.Student,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = workspaceId,
            Data = "{}",
            LocaleDefault = "fa-IR",
            TimeZone = "Asia/Tehran",
            FeatureFlags = """{"gamification":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        await DefaultStoreSeed.EnsureAsync(db);
        return (sp, workspaceId, user.Id);
    }

    private static async Task<int> AddUserAsync(AppDbContext db, Guid workspaceId, string username, string displayName, WorkspaceMemberRole role)
    {
        var user = new UserAccount
        {
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            PasswordHash = "x",
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = user.Id,
            Role = role,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task SeedXpAsync(AppDbContext db, Guid workspaceId, int userId, int xp, DateTime? at = null)
    {
        var when = at ?? DateTime.UtcNow;
        var stats = await db.UserGameStats.FirstOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId);
        if (stats is null)
        {
            stats = new UserGameStats
            {
                WorkspaceId = workspaceId,
                UserId = userId,
                Level = 1,
                TimeZone = "Asia/Tehran",
                UpdatedAt = when,
            };
            db.UserGameStats.Add(stats);
        }

        stats.XpTotal += xp;
        stats.Coins += Math.Max(0, xp);
        stats.UpdatedAt = when;
        db.XpLedger.Add(new XpLedger
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Delta = xp,
            BalanceAfter = stats.XpTotal,
            Reason = "test",
            Kind = LedgerKind.Earn,
            CreatedAt = when,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Leaderboard_ranks_by_xp_and_hides_private_users()
    {
        var (sp, workspaceId, userA) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var boards = scope.ServiceProvider.GetRequiredService<LeaderboardService>();
        var userB = await AddUserAsync(db, workspaceId, "b", "بازیکن ب", WorkspaceMemberRole.Student);

        await SeedXpAsync(db, workspaceId, userA, 30);
        await SeedXpAsync(db, workspaceId, userB, 50);

        await boards.UpdatePrivacyAsync(workspaceId, userB, leaderboardVisible: false, showRealName: true, CancellationToken.None);
        var json = JsonSerializer.Serialize(await boards.GetBoardAsync(workspaceId, LeaderboardMetric.WeeklyXp, "week", null, null, 1, 20, CancellationToken.None));
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal(userA, entries[0].GetProperty("userId").GetInt32());
        Assert.Equal("بازیکن اجتماعی", entries[0].GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Improvement_metric_excludes_non_100_scale_and_cross_subject()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var boards = scope.ServiceProvider.GetRequiredService<LeaderboardService>();

        db.StudyRecords.AddRange(
            new StudyRecord { UserId = userId, WorkspaceId = workspaceId, Collection = "tests", ItemId = "t1", Data = """{"subject":"math","score":60}""" },
            new StudyRecord { UserId = userId, WorkspaceId = workspaceId, Collection = "tests", ItemId = "t2", Data = """{"subject":"math","score":80}""" },
            new StudyRecord { UserId = userId, WorkspaceId = workspaceId, Collection = "tests", ItemId = "t3", Data = """{"subject":"physics","score":90}""" },
            new StudyRecord { UserId = userId, WorkspaceId = workspaceId, Collection = "tests", ItemId = "t4", Data = """{"subject":"math","score":250}""" });
        await db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(await boards.GetBoardAsync(
            workspaceId, LeaderboardMetric.LearningImprovement, "all", "math", null, 1, 20, CancellationToken.None));
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal(2000, entries[0].GetProperty("score").GetInt32()); // (80-60)*100
    }

    [Fact]
    public async Task Team_invite_and_membership_boundaries()
    {
        var (sp, workspaceId, ownerId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var teams = scope.ServiceProvider.GetRequiredService<TeamService>();
        var memberId = await AddUserAsync(db, workspaceId, "mate", "هم‌تیمی", WorkspaceMemberRole.Student);
        var outsider = new UserAccount
        {
            Username = "out",
            NormalizedUsername = "out",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(outsider);
        await db.SaveChangesAsync();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await teams.CreateAsync(workspaceId, ownerId, "تیم آلفا", null, null, CancellationToken.None)));
        var teamId = created.RootElement.GetProperty("id").GetGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => teams.InviteAsync(workspaceId, ownerId, teamId, outsider.Id, CancellationToken.None));
        await teams.InviteAsync(workspaceId, ownerId, teamId, memberId, CancellationToken.None);
        var invite = await db.TeamInvites.SingleAsync();
        await teams.RespondInviteAsync(workspaceId, memberId, invite.Id, true, CancellationToken.None);
        Assert.Equal(2, await db.TeamMembers.CountAsync(item => item.TeamId == teamId && item.LeftAt == null));
    }

    [Fact]
    public async Task Store_purchase_rejects_insufficient_coins_and_duplicate_non_stackable()
    {
        var (sp, workspaceId, userId) = await CreateAsync();
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<StoreService>();

        db.UserGameStats.Add(new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            Level = 1,
            Coins = 100,
            TimeZone = "Asia/Tehran",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var item = await db.StoreItems.FirstAsync(row => row.Code == "glasses");
        await store.PurchaseAsync(workspaceId, userId, item.Id, CancellationToken.None);
        Assert.Equal(45, (await db.UserGameStats.SingleAsync()).Coins);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PurchaseAsync(workspaceId, userId, item.Id, CancellationToken.None));

        var expensive = await db.StoreItems.FirstAsync(row => row.Code == "frame-gold");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PurchaseAsync(workspaceId, userId, expensive.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Physical_reward_requires_approval_before_fulfillment()
    {
        var (sp, workspaceId, adminId) = await CreateAsync(WorkspaceType.School);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rewards = scope.ServiceProvider.GetRequiredService<RewardService>();
        var studentId = await AddUserAsync(db, workspaceId, "stu", "دانش‌آموز", WorkspaceMemberRole.Student);

        db.UserGameStats.Add(new UserGameStats
        {
            WorkspaceId = workspaceId,
            UserId = studentId,
            Level = 1,
            Coins = 0,
            TimeZone = "Asia/Tehran",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = JsonDocument.Parse(JsonSerializer.Serialize(await rewards.CreateAsync(
            workspaceId, adminId, studentId, RewardGrantKind.Physical, "مدال مدرسه", null, 10, null, null, null, CancellationToken.None)));
        Assert.Equal("Pending", created.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, (await db.UserGameStats.SingleAsync(item => item.UserId == studentId)).Coins);

        var grantId = created.RootElement.GetProperty("id").GetGuid();
        await rewards.ApproveAsync(workspaceId, adminId, grantId, true, CancellationToken.None);
        Assert.Equal(10, (await db.UserGameStats.SingleAsync(item => item.UserId == studentId)).Coins);
        Assert.Equal(RewardGrantStatus.Fulfilled, (await db.RewardGrants.SingleAsync()).Status);
    }

    [Fact]
    public async Task Class_ranking_only_includes_class_members()
    {
        var (sp, workspaceId, teacherId) = await CreateAsync(WorkspaceType.School);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var classes = scope.ServiceProvider.GetRequiredService<ClassService>();
        var boards = scope.ServiceProvider.GetRequiredService<LeaderboardService>();

        var inClass = await AddUserAsync(db, workspaceId, "in", "داخل کلاس", WorkspaceMemberRole.Student);
        var outClass = await AddUserAsync(db, workspaceId, "outc", "خارج کلاس", WorkspaceMemberRole.Student);
        var created = JsonDocument.Parse(JsonSerializer.Serialize(await classes.CreateAsync(workspaceId, teacherId, "۱۰۱", CancellationToken.None)));
        var classId = created.RootElement.GetProperty("id").GetGuid();
        await classes.AddMemberAsync(workspaceId, classId, inClass, ClassMemberRole.Student, CancellationToken.None);

        await SeedXpAsync(db, workspaceId, inClass, 40);
        await SeedXpAsync(db, workspaceId, outClass, 90);

        var json = JsonSerializer.Serialize(await boards.GetBoardAsync(
            workspaceId, LeaderboardMetric.Class, "week", null, classId, 1, 20, CancellationToken.None));
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.GetProperty("entries").EnumerateArray().Select(item => item.GetProperty("userId").GetInt32()).ToHashSet();
        Assert.Contains(inClass, entries);
        Assert.DoesNotContain(outClass, entries);
    }

    [Fact]
    public async Task Personal_workspace_cannot_see_other_workspace_boards()
    {
        var (spA, workspaceA, userA) = await CreateAsync();
        var (spB, workspaceB, userB) = await CreateAsync();
        await using var scopeA = spA.CreateAsyncScope();
        await using var scopeB = spB.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var boardsA = scopeA.ServiceProvider.GetRequiredService<LeaderboardService>();
        var boardsB = scopeB.ServiceProvider.GetRequiredService<LeaderboardService>();

        await SeedXpAsync(dbA, workspaceA, userA, 70);
        await SeedXpAsync(scopeB.ServiceProvider.GetRequiredService<AppDbContext>(), workspaceB, userB, 120);

        var json = JsonSerializer.Serialize(await boardsA.GetBoardAsync(
            workspaceA, LeaderboardMetric.WeeklyXp, "week", null, null, 1, 20, CancellationToken.None));
        using var doc = JsonDocument.Parse(json);
        var ids = doc.RootElement.GetProperty("entries").EnumerateArray().Select(item => item.GetProperty("userId").GetInt32()).ToList();
        Assert.Equal([userA], ids);

        var other = JsonSerializer.Serialize(await boardsB.GetBoardAsync(
            workspaceB, LeaderboardMetric.WeeklyXp, "week", null, null, 1, 20, CancellationToken.None));
        using var otherDoc = JsonDocument.Parse(other);
        Assert.Equal(userB, otherDoc.RootElement.GetProperty("entries")[0].GetProperty("userId").GetInt32());
    }
}
