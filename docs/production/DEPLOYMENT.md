# Deployment Guide

## Prerequisites

- .NET 10 runtime
- SQL Server (or LocalDB for non-prod)
- Reverse proxy terminating TLS (recommended)

## Configuration

Set secrets via environment variables or a secret store — **never** commit production credentials.

| Setting | Env var example | Notes |
|---------|-----------------|-------|
| Connection string | `ConnectionStrings__KonkoorHelp` | Required in Production |
| AI provider | `Ai__DefaultProvider` | `heuristic` / `null` / OpenAI-compatible |
| OpenAI key | `Ai__OpenAiApiKey` | Optional; leave empty if unused |
| Allowed Origins | `Security__AllowedOrigins__0` | Extra browser origins beyond same-host + Vite |

Production refuses to start if the connection string is empty.

## Release steps

1. Backup database (see `DATABASE_MIGRATIONS.md`).
2. Deploy new binaries / container image.
3. Start app: `DatabaseBootstrap` runs `MigrateAsync` (additive only) then seeds defaults.
4. Smoke: `GET /health/live`, `GET /health/ready`, login, create a study session.
5. Confirm webhooks/outbox workers are running (hosted services).

## Rollback

1. Stop new instances.
2. Redeploy previous binary/image.
3. If a migration was applied and is reversible, `dotnet ef database update <PreviousMigration>` against a restore point — prefer restore from backup for safety.
4. Verify `/health/ready` and critical user flows.

Do **not** run destructive migrations or drop databases in production automation.
