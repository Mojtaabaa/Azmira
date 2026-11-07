using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}"[..Math.Min(80, Environment.MachineName.Length + 33)];
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                await Task.Delay(processed > 0 ? 250 : 1500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Outbox processor loop failed");
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pipeline = scope.ServiceProvider.GetRequiredService<GamificationPipeline>();

        var now = DateTime.UtcNow;
        var candidates = await db.DomainOutbox
            .Where(item => item.Status == OutboxStatus.Pending
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now)
                && (item.LeaseUntil == null || item.LeaseUntil < now))
            .OrderBy(item => item.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var item in candidates)
        {
            item.LeaseOwner = _leaseOwner;
            item.LeaseUntil = now.AddSeconds(30);
        }

        if (candidates.Count > 0) await db.SaveChangesAsync(cancellationToken);

        try
        {
            return await pipeline.ProcessPendingAsync(cancellationToken, BatchSize);
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Outbox batch failed");
            return 0;
        }
    }
}
