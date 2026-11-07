# Webhooks

## Register

Session + workspace required:

- `POST /api/integrations/webhooks` `{ "url": "https://…", "eventTypes": ["StudySessionCompleted", …] }`
- `GET /api/integrations/webhooks`
- `POST /api/integrations/webhooks/{id}/deactivate`
- `GET /api/integrations/webhooks/deliveries?endpointId=`

HTTPS required except `localhost` / `127.0.0.1`. Secret returned once at creation.

## Events

| Event | Source |
|-------|--------|
| `StudySessionCompleted` | Study write / outbox |
| `QuestCompleted` | Quest engine |
| `BadgeAwarded` | Badge engine |
| `LevelUp` | Level engine |
| `AssessmentCompleted` | Mapped from `AssessmentSubmitted` / `AssessmentPassed` |
| `WeeklyReportGenerated` | AI weekly report |

Subscribe with exact names or `*`.

## Delivery

- Method: `POST` JSON body
- Headers: `X-Rotbeh-Signature: sha256={hex}`, `X-Rotbeh-Event`, `X-Rotbeh-Delivery`
- Signature: HMAC-SHA256 of raw body with endpoint secret
- Retries: exponential backoff (2^attempts minutes, capped at 60), max 8 attempts → `DeadLetter`
- Logs: `WebhookDeliveries` (status, attempts, HTTP code, error)

Verify with constant-time compare of the header against `HMACSHA256(secret, body)`.
