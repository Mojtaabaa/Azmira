# Migration Strategy

## Goals

- Zero downtime preference for `https://rotbeh.viannacloud.ir`
- Preserve all existing user study/notes data
- Make Personal workspace the default path with **no school requirement**
- Move from `EnsureCreated` to EF Core migrations before structural changes

## Non-negotiables

1. No drop of existing tables without explicit written approval.
2. Additive schema first; tighten constraints only after backfill verification.
3. Feature flags gate school/gamification UI until data cutover is verified.
4. Legacy Node/SQLite stack is not migrated in-place; rewrite SQL Server is SoR.

## Phase A — Engineering readiness (no product behavior change)

1. Add EF Core Migrations to `backend`.
2. Baseline migration that snapshots **current** model (Users, AuthSessions, Settings, StudyRecords, Notes*).
3. Disable reliance on `EnsureCreated` in Production (keep only for fresh local DBs if needed).
4. Expand automated smoke tests (auth isolation, CRUD session).

**Rollback:** redeploy previous package; DB unchanged if baseline only.

## Phase B — Workspace foundation (backward compatible)

1. Create tables: Workspaces, WorkspaceMembers, WorkspaceSettings, WorkspaceSubscriptions, (optional Schools empty).
2. Add nullable `WorkspaceId` to StudyRecords + Notes tables.
3. Backfill job/API:
   - For each user: ensure Personal workspace + owner membership + settings copy from `Settings.Data` + Free subscription
   - Set `WorkspaceId` on all that user’s study/notes rows
4. API defaults active workspace to Personal when header missing.
5. Deploy frontend that still works without switcher.

**Verification queries (conceptual):**

- Every user has exactly one Personal workspace
- No StudyRecords with null WorkspaceId remain
- Login/me works; existing sessions still load

**Rollback:** keep nullable WorkspaceId; stop writing new tables; app can ignore workspace.

## Phase C — Enforce workspace NOT NULL + indexes

1. Set `WorkspaceId` NOT NULL on owned tables.
2. Add indexes from `DATABASE_DESIGN.md`.
3. Optionally adjust uniqueness to `(WorkspaceId, Collection, ItemId)` after confirming no conflicts.

**Risk:** unique key change can fail if duplicates exist across edge cases — run duplicate report first.

## Phase D — Outbox + gamification tables

1. Add DomainOutbox, ProcessedEvents, XpLedger, UserGameStats, profiles.
2. Emit events on new study writes only (not historical import).
3. Enable XP UI behind feature flag.

**Risk:** import/export must not grant XP storms — flag `emitEvents:false`.

## Phase E — School modules

1. Schools, invites, classes, guardian links.
2. School workspace creation flows.
3. RBAC policies.
4. Frontend school/parent shells.

## Phase F — Projections & storage hardening

1. StudySessionFacts / AssessmentAttemptFacts backfill from JSON.
2. Begin note image move to object storage (pointers replace data URLs gradually).

## Data backfill algorithm (Personal)

```
for user in Users:
  if not personalWorkspace(user):
    create workspace(type=Personal, owner=user)
    create member(role=Student, active)
    create settings from Settings.Data or defaults
    create subscription Free/Active
  update StudyRecords set WorkspaceId=ws where UserId=user and WorkspaceId is null
  update Notes* set WorkspaceId=ws where UserId=user and WorkspaceId is null
  set user.LastWorkspaceId=ws
```

Idempotent: safe to re-run.

## API/client compatibility timeline

| Stage | Client expectation |
| --- | --- |
| B | Old clients work (no header) |
| C | Old clients work; new clients may send header |
| D | New gamification endpoints optional |
| E | School routes appear; personal UX unchanged |

## Backup & deploy checklist

1. Full DB backup before each phase
2. Deploy API before UI when adding required headers (we avoid required headers)
3. Smoke: register, login, load data, create session, open notes, export
4. Monitor outbox lag after D

## Backward-compatibility risk register

| Risk | Severity | Mitigation |
| --- | --- | --- |
| EnsureCreated vs migrate conflict on existing prod DB | High | Baseline migration carefully; never EnsureCreated on prod again |
| Null WorkspaceId reads after partial backfill | High | Dual-read fallback: if null, treat as user’s Personal until Phase C |
| Settings double-write (user vs workspace) | Medium | Write both during transition; then workspace-only |
| Duplicate XP from replay/import | High | Idempotency keys + import flag |
| Unique index rebuild locking | Medium | Online index ops / maintenance window |
| Legacy Node DB confusion | Medium | Document SoR; freeze legacy writes |
| Parent/school privacy leaks | High | Deny-by-default resource guards + tests |
| Large note images during backfill | Medium | Backfill notes metadata first; images later |

## Approval gate (required before coding)

Confirm approval of:

- [ ] EF migrations baseline approach
- [ ] Personal workspace auto-provision + backfill
- [ ] Additive `WorkspaceId` then NOT NULL cutover
- [ ] Outbox idempotent XP design
- [ ] Legacy stack freeze

**No implementation until this gate passes.**
