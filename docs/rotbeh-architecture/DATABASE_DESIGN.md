# Database Design

## Principles

1. **Preserve** all existing rewrite tables (`Users`, `AuthSessions`, `Settings`, `StudyRecords`, `Notes`, `NoteFolders`, `NoteTrash`, `NoteImages`, `NoteSettings`).
2. Prefer **additive** columns and new tables; destructive changes require explicit approval.
3. Replace `EnsureCreated` with **EF Core migrations** before applying this design.
4. Soft-delete where business recovery matters; hard-delete for ephemeral sessions/outbox cleanup jobs.

## 1. Existing tables — additive changes

### Users (extend)

| New field | Type | Notes |
| --- | --- | --- |
| DisplayName | `nvarchar(80)` null | |
| Locale | `nvarchar(10)` null | `fa-IR` / `en-US` |
| PlatformRole | `tinyint` | 0=None, 100=PlatformAdmin |
| LastWorkspaceId | GUID null | Preference |
| DeletedAt | `datetime2` null | Soft delete account |

### Settings

Keep as legacy personal blob during transition. After Personal workspace exists, treat as **read-through cache** or migrate JSON into `WorkspaceSettings.Data` and stop writing user-level settings.

### StudyRecords / Notes* tables

| New field | Type | Notes |
| --- | --- | --- |
| WorkspaceId | GUID null→NOT NULL | Backfilled to Personal workspace |
| DeletedAt | `datetime2` null | Optional soft delete for study rows |

**Indexes (new):**

- `IX_StudyRecords_Workspace_Collection_Seq (WorkspaceId, Collection, Seq)`
- Unique remains `(UserId, Collection, ItemId)` initially; evolve to `(WorkspaceId, Collection, ItemId)` after cutover (approved migration).

### AuthSessions

Unchanged structurally; optional `WorkspaceId` snapshot not required.

## 2. New core identity / workspace tables

See field lists in `WORKSPACE_DESIGN.md` for:

- `Workspaces`
- `WorkspaceMembers`
- `WorkspaceSettings`
- `WorkspaceSubscriptions`
- `Schools` (Id, Name, Status, OwnerUserId, CreatedAt, …)
- `SchoolInvites` (token hash, role, expires, workspaceId)
- `GuardianStudentLinks` (ParentUserId, StudentUserId, WorkspaceId, ConsentStatus)

## 3. Study projections (optional but recommended)

Additive read-optimized tables populated from study writes/events:

### StudySessionFacts

| Field | Type |
| --- | --- |
| Id | GUID PK |
| WorkspaceId | GUID |
| UserId | int |
| SourceItemId | nvarchar(100) |
| SubjectId | nvarchar(90) null |
| Topic | nvarchar(200) null |
| StartedAt | datetime2 |
| DurationMinutes | int |
| CreatedAt | datetime2 |

Unique: `(WorkspaceId, SourceItemId)`  
Indexes: `(WorkspaceId, UserId, StartedAt)`, `(WorkspaceId, StartedAt)`

### AssessmentAttemptFacts

Similar projection for tests: scores, subject, date, workspace, user.

These do **not** replace JSON documents initially.

## 4. Domain events / outbox

### DomainOutbox

| Field | Type | Notes |
| --- | --- | --- |
| Id | GUID PK | Also used as EventId |
| WorkspaceId | GUID | |
| UserId | int null | |
| EventType | nvarchar(80) | |
| EventVersion | int | |
| OccurredAt | datetime2 | |
| PayloadJson | nvarchar(max) | |
| IdempotencyKey | nvarchar(120) | Business key |
| Status | tinyint | Pending, Processed, Failed |
| Attempts | int | |
| NextAttemptAt | datetime2 null | |
| ProcessedAt | datetime2 null | |
| LastError | nvarchar(1000) null | |
| CreatedAt | datetime2 | |

**Unique:** `(WorkspaceId, EventType, IdempotencyKey)`

### DomainInbox / ProcessedEvents (consumer side)

| Field | Type |
| --- | --- |
| ConsumerName | nvarchar(80) |
| EventId | GUID |
| ProcessedAt | datetime2 |

PK: `(ConsumerName, EventId)` — guarantees one XP award per event per consumer.

## 5. Gamification / rewards

- `GamificationProfiles` (Id, Code, IsSystem, RulesJson, CreatedAt)
- `XpLedger` (Id, WorkspaceId, UserId, EventId unique, Delta, BalanceAfter, Reason, CreatedAt)
- `UserGameStats` (WorkspaceId, UserId PK, XpTotal, Level, StreakCurrent, StreakBest, UpdatedAt)
- `Badges`, `UserBadges`
- `Quests`, `UserQuestProgress`
- `RewardItems`, `UserInventory`
- `LeaderboardSnapshots` (WorkspaceId, Period, PayloadJson, GeneratedAt)

All user progress tables include **WorkspaceId**.

## 6. School / assessment / notifications / integrations (summary)

| Table group | Purpose |
| --- | --- |
| Classes, ClassMembers | Teacher-student grouping |
| Assignments, AssignmentSubmissions | School assessment loop |
| Notifications, NotificationDeliveries | In-app inbox |
| ApiClients, ApiClientWorkspaces | Public API keys |
| WebhookEndpoints, WebhookDeliveries | Outbound integrations |
| AuditLogs | Security-sensitive actions |
| AiConversations, AiMessages | Opt-in coach |

Detailed columns for these groups can be finalized per implementation slice; WorkspaceId/SchoolId ownership is mandatory.

## 7. Soft deletion strategy

| Entity | Soft delete? |
| --- | --- |
| Users, Workspaces, Members | Yes |
| StudyRecords / Notes | Optional; prefer trash semantics already used for notes |
| Outbox processed rows | Archive/purge job (hard delete after N days) |
| XpLedger | **No** soft delete of awarded rows; reverse via compensating ledger entry |

## 8. Tenant ownership strategy

- Personal data: Workspace Type=Personal, Member=owner
- School data: Workspace Type=School
- Cross-link tables (guardian links) always include WorkspaceId of the school context

## 9. Type mapping conventions

| Concept | SQL |
| --- | --- |
| Ids (new) | `uniqueidentifier` |
| Existing user ids | `int` (preserve) |
| Money/XP | `int` |
| Enums | `tinyint` + app enum |
| JSON | `nvarchar(max)` |
| Timestamps | `datetime2` UTC |

## 10. Migration safety

Order of operations is specified in `MIGRATION_STRATEGY.md`. No migration is executed in Phase 1.
