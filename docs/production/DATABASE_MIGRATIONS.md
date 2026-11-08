# Database Migrations

## Strategy

- EF Core migrations under `backend/Data/Migrations/`.
- On startup: `DatabaseBootstrap.ApplyAsync` → `MigrateAsync` (additive).
- Legacy EnsureCreated DBs are stamped then upgraded — no drop/recreate.

Phase 7 migration: `20261107065105_AddIntegrations` (ApiClients, ApiClientWorkspaces, WebhookEndpoints, WebhookDeliveries).

Experience Beta (Phases 9–13) additive migrations — see also `docs/product/PHASE_14_BETA_CHECKLIST.md`:

- `AddLearningJourney`
- `AddPersonalImprovementAndCoach`
- `AddJourneyPackPublishFlags`

(Plus earlier gamification / school / intelligence migrations already on main.)

## Commands (ops)

```bash
cd backend
dotnet ef migrations list
dotnet ef database update                 # apply pending
dotnet ef database update <MigrationName> # target specific (rollback when Down exists)
```

Never use `EnsureDeleted` / drop-database in production scripts.

## Backup / restore

**Backup (SQL Server example):**

```sql
BACKUP DATABASE [KonkoorHelp] TO DISK = N'C:\Backups\KonkoorHelp.bak' WITH INIT, COMPRESSION;
```

**Restore:**

```sql
RESTORE DATABASE [KonkoorHelp] FROM DISK = N'C:\Backups\KonkoorHelp.bak' WITH REPLACE;
```

Take a backup before every production deploy that applies migrations. Prefer restore over manual reverse-engineering if something fails mid-migration.
