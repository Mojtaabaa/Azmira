# Webhooks

## Authorization

Only workspace administrators may create, list, deactivate, or view deliveries:

- Personal workspace: owner only
- School workspace: `SchoolOwner` / `SchoolAdministrator` (platform admins also allowed)

Cross-workspace management is rejected (`403`).

## Register

Session + workspace required:

- `POST /api/integrations/webhooks` `{ "url": "https://…", "eventTypes": ["StudySessionCompleted", …] }`
- `GET /api/integrations/webhooks`
- `POST /api/integrations/webhooks/{id}/deactivate`
- `GET /api/integrations/webhooks/deliveries?endpointId=`

**HTTPS only** to public destinations. Loopback, private, link-local, multicast, reserved, and cloud-metadata addresses are rejected (IPv4 and IPv6). DNS is resolved at registration **and** again at delivery; **every** resolved address must be public — a mixed public/private answer is rejected. The outbound TCP connection is pinned to a validated IP via `SocketsHttpHandler.ConnectCallback` while the original hostname is preserved for the HTTP `Host` header, TLS SNI, and certificate hostname validation. TLS certificate validation is never disabled. Connection pooling is disabled for the webhook client so a later pin cannot be skipped. HTTP redirects are disabled. If pinning cannot be established, delivery fails closed.

Secret is returned once at creation. At rest it is stored with AES-GCM (`v1.…`) using `Integrations__WebhookProtectionKey`. List/detail APIs never return the secret. Legacy plaintext rows remain readable until rotated.

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
- Logs: `WebhookDeliveries` (status, attempts, HTTP code, error) — no payload/secret fields

Verify with constant-time compare of the header against `HMACSHA256(secret, body)`.

## Secret key rotation

1. Generate a new 32-byte key: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`.
2. Set `Integrations__WebhookProtectionKey` (or `ROTBEH_WEBHOOK_PROTECTION_KEY`) via the secret store — never commit it.
3. Redeploy. Existing `v1.` ciphertext encrypted with the **old** key cannot be decrypted after rotation; recreate webhook endpoints (or run a controlled re-encrypt job offline with both keys).
4. Legacy plaintext secrets still work until deactivated/recreated; prefer recreate after enabling encryption.
