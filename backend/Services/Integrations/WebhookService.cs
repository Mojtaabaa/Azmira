using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KonkoorHelp.Api.Data;
using KonkoorHelp.Api.Services.Gamification;
using Microsoft.EntityFrameworkCore;

namespace KonkoorHelp.Api.Services.Integrations;

public sealed class WebhookService(AppDbContext db, IHttpClientFactory httpClientFactory, ILogger<WebhookService> logger)
{
    public static readonly string[] SupportedEvents =
    [
        DomainEventTypes.StudySessionCompleted,
        DomainEventTypes.QuestCompleted,
        WebhookEventTypes.BadgeAwarded,
        WebhookEventTypes.LevelUp,
        WebhookEventTypes.AssessmentCompleted,
        DomainEventTypes.AssessmentSubmitted,
        WebhookEventTypes.WeeklyReportGenerated,
    ];

    public async Task<object> CreateEndpointAsync(
        Guid workspaceId,
        int userId,
        string url,
        IEnumerable<string> eventTypes,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new InvalidOperationException("URL وب‌هوک نامعتبر است.");
        if (uri.Scheme == "http" && uri.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("در محیط غیرمحلی فقط HTTPS مجاز است.");

        var events = eventTypes.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (events.Length == 0) throw new InvalidOperationException("حداقل یک نوع رویداد لازم است.");

        var endpoint = new WebhookEndpoint
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Url = url.Trim(),
            Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            EventTypesJson = JsonSerializer.Serialize(events),
            Active = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.WebhookEndpoints.Add(endpoint);
        await db.SaveChangesAsync(cancellationToken);
        return new
        {
            id = endpoint.Id,
            url = endpoint.Url,
            secret = endpoint.Secret,
            eventTypes = events,
            note = "secret را برای تأیید امضای X-Rotbeh-Signature نگه دار.",
        };
    }

    public async Task<object> DeactivateEndpointAsync(Guid workspaceId, Guid endpointId, CancellationToken cancellationToken)
    {
        var endpoint = await db.WebhookEndpoints.FirstOrDefaultAsync(
            item => item.Id == endpointId && item.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("وب‌هوک پیدا نشد.");
        endpoint.Active = false;
        endpoint.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new { ok = true };
    }

    public async Task<object> ListEndpointsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var items = await db.WebhookEndpoints.AsNoTracking()
            .Where(item => item.WorkspaceId == workspaceId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                id = item.Id,
                url = item.Url,
                active = item.Active,
                eventTypes = item.EventTypesJson,
                createdAt = item.CreatedAt,
            })
            .ToListAsync(cancellationToken);
        return new { items, supportedEvents = SupportedEvents };
    }

    public async Task<object> ListDeliveriesAsync(Guid workspaceId, Guid? endpointId, CancellationToken cancellationToken)
    {
        var query = db.WebhookDeliveries.AsNoTracking().Where(item => item.WorkspaceId == workspaceId);
        if (endpointId is Guid id) query = query.Where(item => item.WebhookEndpointId == id);
        var items = await query.OrderByDescending(item => item.CreatedAt).Take(100)
            .Select(item => new
            {
                id = item.Id,
                endpointId = item.WebhookEndpointId,
                eventType = item.EventType,
                status = item.Status.ToString(),
                attempts = item.Attempts,
                lastStatusCode = item.LastStatusCode,
                lastError = item.LastError,
                createdAt = item.CreatedAt,
                deliveredAt = item.DeliveredAt,
            })
            .ToListAsync(cancellationToken);
        return new { items };
    }

    public async Task EnqueueForEventAsync(
        Guid workspaceId,
        Guid? eventId,
        string eventType,
        object payload,
        CancellationToken cancellationToken)
    {
        var endpoints = await db.WebhookEndpoints
            .Where(item => item.WorkspaceId == workspaceId && item.Active)
            .ToListAsync(cancellationToken);
        if (endpoints.Count == 0) return;

        var mappedType = MapEventType(eventType);
        foreach (var endpoint in endpoints)
        {
            if (!Subscribes(endpoint, mappedType) && !Subscribes(endpoint, eventType)) continue;
            var body = JsonSerializer.Serialize(new
            {
                id = eventId ?? Guid.NewGuid(),
                type = mappedType,
                workspaceId,
                occurredAt = DateTime.UtcNow,
                data = payload,
            }, GamificationJson.Options);
            var signature = Sign(endpoint.Secret, body);
            db.WebhookDeliveries.Add(new WebhookDelivery
            {
                Id = Guid.NewGuid(),
                WebhookEndpointId = endpoint.Id,
                WorkspaceId = workspaceId,
                EventId = eventId,
                EventType = mappedType,
                PayloadJson = body,
                Signature = signature,
                Status = WebhookDeliveryStatus.Pending,
                Attempts = 0,
                NextAttemptAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken, int take = 20)
    {
        var now = DateTime.UtcNow;
        var pending = await db.WebhookDeliveries
            .Include(item => item.Endpoint)
            .Where(item => item.Status == WebhookDeliveryStatus.Pending
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
            .OrderBy(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var client = httpClientFactory.CreateClient(nameof(WebhookService));
        var processed = 0;
        foreach (var delivery in pending)
        {
            if (!delivery.Endpoint.Active)
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.LastError = "endpoint inactive";
                processed++;
                continue;
            }

            processed++;
            delivery.Attempts += 1;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, delivery.Endpoint.Url);
                request.Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("X-Rotbeh-Signature", delivery.Signature);
                request.Headers.TryAddWithoutValidation("X-Rotbeh-Event", delivery.EventType);
                request.Headers.TryAddWithoutValidation("X-Rotbeh-Delivery", delivery.Id.ToString("N"));
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RotbehWebhook", "1.0"));

                using var response = await client.SendAsync(request, cancellationToken);
                delivery.LastStatusCode = (int)response.StatusCode;
                if ((int)response.StatusCode is >= 200 and < 300)
                {
                    delivery.Status = WebhookDeliveryStatus.Succeeded;
                    delivery.DeliveredAt = DateTime.UtcNow;
                    delivery.LastError = null;
                }
                else
                {
                    ScheduleRetry(delivery, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Webhook delivery {DeliveryId} failed", delivery.Id);
                ScheduleRetry(delivery, error.Message);
            }
        }

        if (pending.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return processed;
    }

    public static string Sign(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool VerifySignature(string secret, string body, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        var expected = Sign(secret, body);
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(signatureHeader.Trim());
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void ScheduleRetry(WebhookDelivery delivery, string error)
    {
        delivery.LastError = error.Length > 400 ? error[..400] : error;
        if (delivery.Attempts >= 8)
        {
            delivery.Status = WebhookDeliveryStatus.DeadLetter;
            return;
        }

        delivery.Status = WebhookDeliveryStatus.Pending;
        var delayMinutes = Math.Min(60, (int)Math.Pow(2, delivery.Attempts));
        delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    private static bool Subscribes(WebhookEndpoint endpoint, string eventType)
    {
        try
        {
            var types = JsonSerializer.Deserialize<string[]>(endpoint.EventTypesJson) ?? [];
            return types.Contains("*", StringComparer.OrdinalIgnoreCase)
                || types.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string MapEventType(string eventType) => eventType switch
    {
        var t when t.Equals(DomainEventTypes.AssessmentSubmitted, StringComparison.OrdinalIgnoreCase) => WebhookEventTypes.AssessmentCompleted,
        var t when t.Equals(DomainEventTypes.AssessmentPassed, StringComparison.OrdinalIgnoreCase) => WebhookEventTypes.AssessmentCompleted,
        var t when t.Equals(GameNotificationKind.BadgeUnlocked.ToString(), StringComparison.OrdinalIgnoreCase) => WebhookEventTypes.BadgeAwarded,
        _ => eventType,
    };
}

public static class WebhookEventTypes
{
    public const string BadgeAwarded = "BadgeAwarded";
    public const string LevelUp = "LevelUp";
    public const string AssessmentCompleted = "AssessmentCompleted";
    public const string WeeklyReportGenerated = "WeeklyReportGenerated";
}
