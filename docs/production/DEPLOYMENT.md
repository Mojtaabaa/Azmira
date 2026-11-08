# Deployment Guide

## Prerequisites

- .NET 10 runtime
- SQL Server (or LocalDB for non-prod)
- Reverse proxy terminating TLS (recommended)

## Configuration

Prefer **`sandbox/appsettings.json`** on the install host (never overwritten by updates). Environment variables still override sandbox when set.

| Setting | Sandbox JSON / Env var | Notes |
|---------|------------------------|-------|
| Connection string | `ConnectionStrings:KonkoorHelp` / `ConnectionStrings__KonkoorHelp` | Required in Production |
| AI provider | `Ai:DefaultProvider` | `heuristic` / `null` / OpenAI-compatible |
| OpenAI key | `Ai:OpenAiApiKey` | Optional; leave empty if unused |
| Allowed Origins | `Security:AllowedOrigins` | Extra browser origins beyond same-host + Vite |
| Webhook secret key | `Integrations:WebhookProtectionKey` | Base64 32-byte AES-GCM key; required in Production |

See [`sandbox/README.md`](../../sandbox/README.md) and `sandbox/PRESERVE.manifest.json` for the preserve list.

Production refuses to start if the connection string is empty.

## Release steps

1. Backup database (see `DATABASE_MIGRATIONS.md`).
2. Build: `.\publish.ps1` → `publish/`.
3. Update site (keeps sandbox secrets):
   ```powershell
   .\publish\install-rotbeh.ps1 -Source .\publish -Target C:\inetpub\Rotbeh
   ```
4. First install only: copy `sandbox\appsettings.json.example` → `sandbox\appsettings.json` and fill secrets (installer does this if missing).
5. Start app: merges any **new** keys from `sandbox/appsettings.defaults.json` into live sandbox settings, then `MigrateAsync`.
6. Smoke: `GET /health/live`, `GET /health/ready`, login, create a study session.
7. Confirm webhooks/outbox workers are running (hosted services).

## Rollback

1. Stop new instances.
2. Redeploy previous binary/image.
3. If a migration was applied and is reversible, `dotnet ef database update <PreviousMigration>` against a restore point — prefer restore from backup for safety.
4. Verify `/health/ready` and critical user flows.

Do **not** run destructive migrations or drop databases in production automation.
