using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using KonkoorHelp.Api.Services.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KonkoorHelp.Tests;

public class LearningJourneyTests
{
    private static ServiceProvider Build(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => TestDbOptions.UseInMemory(options, dbName));
        services.AddScoped<OutboxPublisher>();
        services.AddScoped<JourneyService>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static async Task<(Guid Ws, int UserId)> SeedAsync(ServiceProvider sp)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new UserAccount
        {
            Username = "j" + Guid.NewGuid().ToString("N")[..8],
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
            Type = WorkspaceType.Personal,
            Name = "j",
            OwnerUserId = user.Id,
            Status = WorkspaceStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (ws, user.Id);
    }

    [Fact]
    public async Task Bootstrap_creates_regions_from_mastery_subjects()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Subject = "ریاضی",
            Topic = "معادله",
            Level = MasteryLevel.Learning,
            ScoreEma = 0.2,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var map = JsonSerializer.SerializeToElement(await journeys.GetMeAsync(ws, userId, CancellationToken.None));
        Assert.True(map.GetProperty("isBootstrap").GetBoolean());
        Assert.True(map.GetProperty("regions").GetArrayLength() >= 1);
        Assert.Contains("Evidence", map.GetProperty("unlockPolicy").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Mastery_evidence_unlocks_next_stage_not_xp()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            UserId = userId,
            Subject = "فیزیک",
            Topic = "نیرو",
            Level = MasteryLevel.Developing,
            ScoreEma = 0.6,
            UpdatedAt = DateTime.UtcNow,
            LastEvidenceAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = JsonSerializer.SerializeToElement(await journeys.EvaluateAsync(ws, userId, CancellationToken.None));
        var newly = result.GetProperty("newlyUnlockedStageIds");
        Assert.True(newly.GetArrayLength() >= 1);

        var map = result.GetProperty("map");
        var stages = map.GetProperty("regions")[0].GetProperty("stages");
        var unlockedOrComplete = 0;
        foreach (var stage in stages.EnumerateArray())
        {
            var state = stage.GetProperty("state").GetString();
            if (state is "available" or "complete") unlockedOrComplete++;
            if (stage.GetProperty("evidence").GetArrayLength() > 0)
            {
                Assert.DoesNotContain("xp", stage.GetProperty("evidence")[0].GetProperty("type").GetString()!, StringComparison.OrdinalIgnoreCase);
            }
        }
        Assert.True(unlockedOrComplete >= 1);
    }

    [Fact]
    public async Task Rejected_requirements_json_with_only_xp_does_not_unlock()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        db.MasteryNodes.AddRange(
            new MasteryNode
            {
                Id = Guid.NewGuid(), WorkspaceId = ws, UserId = userId, Subject = "شیمی", Topic = "اتم",
                Level = MasteryLevel.Mastered, ScoreEma = 0.9, UpdatedAt = DateTime.UtcNow,
            },
            new MasteryNode
            {
                Id = Guid.NewGuid(), WorkspaceId = ws, UserId = userId, Subject = "شیمی", Topic = "پیوند",
                Level = MasteryLevel.Mastered, ScoreEma = 0.9, UpdatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        await journeys.GetMeAsync(ws, userId, CancellationToken.None);
        var stage = await db.JourneyStages.OrderBy(s => s.SortOrder).Skip(1).FirstAsync();
        stage.RequirementsJson = """{"xp":100,"study_minutes":30}""";
        stage.UnlockPriorStageId = null;
        await db.SaveChangesAsync();

        var progress = await db.LearnerJourneyProgress.FirstAsync();
        progress.UnlockedStageIdsJson = "[]";
        progress.CompletedStageIdsJson = "[]";
        await db.SaveChangesAsync();

        var journey = await db.JourneyDefinitions.Include(j => j.Regions).ThenInclude(r => r.Stages)
            .FirstAsync(j => j.Id == progress.JourneyId);
        var newly = await journeys.EvaluateUnlocksAsync(ws, userId, journey, progress, CancellationToken.None);
        Assert.DoesNotContain(stage.Id, newly);
    }

    [Fact]
    public void Bootstrap_code_prefix_is_stable()
    {
        Assert.Equal("bootstrap", JourneyService.BootstrapCodePrefix);
    }

    [Fact]
    public async Task Workspace_isolation_keeps_progress_and_definitions_separate()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (wsA, userA) = await SeedAsync(sp);
        var (wsB, userB) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        db.MasteryNodes.Add(new MasteryNode
        {
            Id = Guid.NewGuid(),
            WorkspaceId = wsA,
            UserId = userA,
            Subject = "جبر",
            Topic = "تابع",
            Level = MasteryLevel.Proficient,
            ScoreEma = 0.8,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await journeys.GetMeAsync(wsA, userA, CancellationToken.None);
        await journeys.GetMeAsync(wsB, userB, CancellationToken.None);

        Assert.Equal(1, await db.JourneyDefinitions.CountAsync(j => j.WorkspaceId == wsA));
        Assert.Equal(1, await db.JourneyDefinitions.CountAsync(j => j.WorkspaceId == wsB));
        Assert.Equal(0, await db.MasteryNodes.CountAsync(n => n.WorkspaceId == wsB));
        var mapB = JsonSerializer.SerializeToElement(await journeys.GetMeAsync(wsB, userB, CancellationToken.None));
        // B has no mastery evidence — only always-open bootstrap stages complete
        foreach (var region in mapB.GetProperty("regions").EnumerateArray())
        {
            foreach (var stage in region.GetProperty("stages").EnumerateArray())
            {
                if (stage.GetProperty("sortOrder").GetInt32() > 0)
                    Assert.Equal("locked", stage.GetProperty("state").GetString());
            }
        }
    }

    [Fact]
    public async Task Unlock_publishes_outbox_once_and_cosmetic_hook()
    {
        var sp = Build(Guid.NewGuid().ToString("N"));
        var (ws, userId) = await SeedAsync(sp);
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var journeys = scope.ServiceProvider.GetRequiredService<JourneyService>();

        db.MasteryNodes.AddRange(
            new MasteryNode
            {
                Id = Guid.NewGuid(), WorkspaceId = ws, UserId = userId, Subject = "هندسه", Topic = "زاویه",
                Level = MasteryLevel.Developing, ScoreEma = 0.55, UpdatedAt = DateTime.UtcNow,
            },
            new MasteryNode
            {
                Id = Guid.NewGuid(), WorkspaceId = ws, UserId = userId, Subject = "هندسه", Topic = "مثلث",
                Level = MasteryLevel.Proficient, ScoreEma = 0.7, UpdatedAt = DateTime.UtcNow,
            },
            new MasteryNode
            {
                Id = Guid.NewGuid(), WorkspaceId = ws, UserId = userId, Subject = "هندسه", Topic = "دایره",
                Level = MasteryLevel.Mastered, ScoreEma = 0.85, UpdatedAt = DateTime.UtcNow,
            });
        var assessmentId = Guid.NewGuid();
        db.Assessments.Add(new AssessmentDefinition
        {
            Id = assessmentId,
            WorkspaceId = ws,
            Title = "هندسه ۱",
            Subject = "هندسه",
            Kind = AssessmentKind.SelfAssessment,
            Status = AssessmentStatus.Published,
            CreatedByUserId = userId,
            PassScore = 70,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.AssessmentAttempts.Add(new AssessmentAttempt
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ws,
            AssessmentId = assessmentId,
            UserId = userId,
            Status = AttemptStatus.Submitted,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            SubmittedAt = DateTime.UtcNow,
            Score = 90,
            MaxScore = 100,
            Percent = 90,
        });
        await db.SaveChangesAsync();

        await journeys.EvaluateAsync(ws, userId, CancellationToken.None);
        await journeys.EvaluateAsync(ws, userId, CancellationToken.None);

        var unlockedEvents = await db.DomainOutbox
            .Where(e => e.EventType == DomainEventTypes.JourneyStageUnlocked && e.WorkspaceId == ws)
            .ToListAsync();
        Assert.NotEmpty(unlockedEvents);
        Assert.Equal(unlockedEvents.Count, unlockedEvents.Select(e => e.IdempotencyKey).Distinct().Count());

        Assert.True(await db.GameNotifications.AnyAsync(n =>
            n.WorkspaceId == ws && n.UserId == userId && n.PayloadJson.Contains("achievementLinked", StringComparison.Ordinal)));
    }
}
