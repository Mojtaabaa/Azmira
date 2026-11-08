using System.Diagnostics;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using KonkoorHelp.Api.Services.Learning.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KonkoorHelp.Tests;

public class Phase13HardeningTests
{
    private static async Task<(ServiceProvider Sp, Guid WorkspaceId, int UserId)> CreateSchoolAsync()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, databaseName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<GamificationProfileService>();
        services.AddScoped<XpEngine>();
        services.AddScoped<LevelEngine>();
        services.AddScoped<BadgeEngine>();
        services.AddScoped<QuestEngine>();
        services.AddScoped<StreakEngine>();
        services.AddScoped<GamificationQueryService>();
        services.AddScoped<GamificationPipeline>();
        services.AddScoped<DailyMissionService>();
        services.AddScoped<JourneyService>();
        services.AddScoped<PersonalImprovementService>();
        services.AddScoped<LearnerHomeService>();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new TestHostEnvironment());
        services.AddSingleton(Options.Create(new KonkoorHelp.Api.Services.Integrations.IntegrationsOptions
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

        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "p13" + Guid.NewGuid().ToString("N")[..6],
            NormalizedUsername = Guid.NewGuid().ToString("N"),
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ws = Guid.NewGuid();
        db.Workspaces.Add(new Workspace
        {
            Id = ws,
            Type = WorkspaceType.School,
            Name = "مدرسه فاز۱۳",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = user.Id,
            Role = WorkspaceMemberRole.SchoolOwner,
            Status = WorkspaceMemberStatus.Active,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.WorkspaceSettings.Add(new WorkspaceSettings
        {
            WorkspaceId = ws,
            FeatureFlags = """{"gamification":true}""",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        await DefaultGamificationSeed.EnsureAsync(db);
        return (sp, ws, user.Id);
    }

    [Fact]
    public async Task School_pack_publish_activate_replaces_bootstrap()
    {
        var (sp, ws, userId) = await CreateSchoolAsync();
        await using var scope = sp.CreateAsyncScope();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        var created = JsonSerializer.SerializeToElement(await journeys.CreatePackAsync(
            ws,
            "pack:math-core",
            "مسیر ریاضی مدرسه",
            "fa-IR",
            [
                new PackRegionInput
                {
                    Title = "ریاضی",
                    SubjectKey = "ریاضی",
                    Stages =
                    [
                        new PackStageInput { Title = "آغاز", AlwaysOpen = true },
                        new PackStageInput { Title = "معادله", Topic = "معادله", MinScoreEma = 0.5 },
                    ],
                },
            ],
            CancellationToken.None));

        var packId = created.GetProperty("id").GetGuid();
        await journeys.PublishPackAsync(ws, packId, CancellationToken.None);
        await journeys.ActivatePackAsync(ws, packId, CancellationToken.None);

        var map = JsonSerializer.SerializeToElement(await journeys.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.False(map.GetProperty("isBootstrap").GetBoolean());
        Assert.True(map.GetProperty("isPublished").GetBoolean());
        Assert.Equal(packId, map.GetProperty("journeyId").GetGuid());
        Assert.Equal("مسیر ریاضی مدرسه", map.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Learner_home_compose_is_fast_under_parallel_load()
    {
        var (sp, ws, userId) = await CreateSchoolAsync();
        await using var scope = sp.CreateAsyncScope();
        var home = scope.ServiceProvider.GetRequiredService<LearnerHomeService>();

        // Warm once
        _ = await home.GetHomeAsync(ws, userId, CancellationToken.None);

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 40)
            .Select(_ => home.GetHomeAsync(ws, userId, CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(tasks);
        sw.Stop();

        Assert.Equal(40, results.Length);
        var sample = JsonSerializer.SerializeToElement(results[0]!);
        Assert.Equal("server", sample.GetProperty("streakSource").GetString());
        Assert.Equal("private", sample.GetProperty("privacy").GetProperty("leaderboardDefault").GetString());
        Assert.True(sw.ElapsedMilliseconds < 15_000, $"Home compose load too slow: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task Pack_activation_is_workspace_scoped()
    {
        var (spA, wsA, userA) = await CreateSchoolAsync();
        var (spB, wsB, _) = await CreateSchoolAsync();
        await using var scopeA = spA.CreateAsyncScope();
        await using var scopeB = spB.CreateAsyncScope();
        var journeysA = scopeA.ServiceProvider.GetRequiredService<JourneyService>();
        var journeysB = scopeB.ServiceProvider.GetRequiredService<JourneyService>();

        var created = JsonSerializer.SerializeToElement(await journeysA.CreatePackAsync(
            wsA,
            "pack:iso",
            "بسته الف",
            null,
            [
                new PackRegionInput
                {
                    SubjectKey = "فیزیک",
                    Stages = [new PackStageInput { Title = "شروع", AlwaysOpen = true }],
                },
            ],
            CancellationToken.None));
        var packId = created.GetProperty("id").GetGuid();
        await journeysA.PublishPackAsync(wsA, packId, CancellationToken.None);
        await journeysA.ActivatePackAsync(wsA, packId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            journeysB.ActivatePackAsync(wsB, packId, CancellationToken.None));

        var ownerB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>().WorkspaceMembers
            .First(m => m.WorkspaceId == wsB).UserId;
        var mapB = JsonSerializer.SerializeToElement(await journeysB.GetMeAsync(wsB, ownerB, CancellationToken.None));
        Assert.True(mapB.GetProperty("isBootstrap").GetBoolean());
    }
}
