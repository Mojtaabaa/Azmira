using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class GamificationProfileService(AppDbContext db)
{
    public async Task<GamificationProfile> ResolveAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var linkedId = await db.WorkspaceSettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .Select(item => item.GamificationProfileId)
            .FirstOrDefaultAsync(cancellationToken);

        if (linkedId is Guid id)
        {
            var linked = await db.GamificationProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (linked is not null) return linked;
        }

        var system = await db.GamificationProfiles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == DefaultGamificationSeed.DefaultProfileCode, cancellationToken);
        if (system is not null) return system;

        await DefaultGamificationSeed.EnsureAsync(db, cancellationToken);
        return await db.GamificationProfiles.AsNoTracking()
            .FirstAsync(item => item.Code == DefaultGamificationSeed.DefaultProfileCode, cancellationToken);
    }

    public ProfileRulesDocument ParseRules(GamificationProfile profile) =>
        JsonSerializer.Deserialize<ProfileRulesDocument>(profile.RulesJson, GamificationJson.Options)
        ?? DefaultGamificationSeed.DefaultRules();

    public List<LevelDefinition> ParseLevels(GamificationProfile profile) =>
        JsonSerializer.Deserialize<List<LevelDefinition>>(profile.LevelsJson, GamificationJson.Options)
        ?? DefaultGamificationSeed.DefaultLevels();

    public StreakSettings ParseStreak(GamificationProfile profile) =>
        JsonSerializer.Deserialize<StreakSettings>(profile.StreakJson, GamificationJson.Options)
        ?? DefaultGamificationSeed.DefaultStreak();

    public async Task<string> ResolveTimeZoneAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var tz = await db.WorkspaceSettings.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .Select(item => item.TimeZone)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(tz) ? "Asia/Tehran" : tz;
    }
}
