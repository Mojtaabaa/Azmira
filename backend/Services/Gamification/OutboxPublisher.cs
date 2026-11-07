using System.Text.Json;
using KonkoorHelp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Gamification;

public sealed class OutboxPublisher(AppDbContext db)
{
    public async Task<Guid?> PublishAsync(
        Guid workspaceId,
        int? userId,
        string eventType,
        string idempotencyKey,
        object data,
        CancellationToken cancellationToken,
        string? correlationId = null,
        int eventVersion = 1,
        DateTime? occurredAt = null)
    {
        var key = idempotencyKey.Length > 120 ? idempotencyKey[..120] : idempotencyKey;
        var existing = await db.DomainOutbox.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId && item.EventType == eventType && item.IdempotencyKey == key)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is Guid existingId) return existingId;

        var id = Guid.NewGuid();
        var occurred = occurredAt ?? DateTime.UtcNow;
        using var dataDoc = JsonDocument.Parse(JsonSerializer.Serialize(data, GamificationJson.Options));
        var envelope = new DomainEventEnvelope
        {
            EventId = id,
            EventType = eventType,
            EventVersion = eventVersion,
            OccurredAt = occurred,
            WorkspaceId = workspaceId,
            ActorUserId = userId,
            IdempotencyKey = key,
            CorrelationId = correlationId,
            Data = dataDoc.RootElement.Clone(),
        };

        db.DomainOutbox.Add(new DomainOutbox
        {
            Id = id,
            WorkspaceId = workspaceId,
            UserId = userId,
            EventType = eventType,
            EventVersion = eventVersion,
            OccurredAt = occurred,
            PayloadJson = JsonSerializer.Serialize(envelope, GamificationJson.Options),
            IdempotencyKey = key,
            Status = OutboxStatus.Pending,
            Attempts = 0,
            NextAttemptAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return id;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return await db.DomainOutbox.AsNoTracking()
                .Where(item => item.WorkspaceId == workspaceId && item.EventType == eventType && item.IdempotencyKey == key)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

}
