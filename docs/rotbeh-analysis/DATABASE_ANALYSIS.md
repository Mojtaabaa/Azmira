# Database Analysis

## Important dual-database note

| Stack | Engine | Schema source |
| --- | --- | --- |
| Rewrite | SQL Server | EF Core model + `EnsureCreated()` |
| Legacy | SQLite file | SQL string in `server/db.js` |

No EF Core **Migrations** folder exists. Schema evolution today is manual / recreate-oriented. **Do not run migrations in Phase 0** (none to apply anyway).

Connection string values are intentionally omitted from this report.

---

## Rewrite schema (SQL Server) — verified entities

### Users

| Column | Notes |
| --- | --- |
| `Id` (PK, identity) | int |
| `Username` | max 32 |
| `NormalizedUsername` | unique index |
| `PasswordHash` | max 512 |
| `CreatedAt` | UTC datetime |

### AuthSessions

| Column | Notes |
| --- | --- |
| `TokenHash` (PK) | SHA-256 hex of cookie token |
| `UserId` (FK → Users, cascade) | indexed |
| `ExpiresAt` | unix ms |

### Settings

| Column | Notes |
| --- | --- |
| `UserId` (PK, FK) | 1:1 with user |
| `Data` | JSON blob of settings object |

### StudyRecords (document store)

| Column | Notes |
| --- | --- |
| `Seq` (PK identity) | ordering |
| `UserId` (FK) | |
| `Collection` | one of sessions/tests/mistakes/topics/tasks/events |
| `ItemId` | client string id |
| `Data` | full JSON document |

Indexes:

- Unique `(UserId, Collection, ItemId)`
- `(UserId, Collection, Seq)`

**Business meaning:** relational tables for study domains are **not** first-class. Domain fields live inside JSON.

### Notes tables

| Table | Key | Payload |
| --- | --- | --- |
| `Notes` | `(UserId, NoteId)` | JSON note document |
| `NoteFolders` | `(UserId, FolderId)` | JSON folder |
| `NoteTrash` | `(UserId, TrashId)` | JSON + `DeletedAt` (+ index) |
| `NoteImages` | `(UserId, ImageId)` | `DataUrl` text + `CreatedAt` |
| `NoteSettings` | `(UserId, Key)` | JSON value string |

---

## Legacy schema (SQLite) — verified

Tables: `users`, `auth_sessions`, `settings`, `records`  
Same conceptual model as rewrite study side (JSON records), without notes tables (notes were client-only).

---

## Relationships (conceptual)

```
User 1──1 Settings
User 1──* AuthSessions
User 1──* StudyRecords
User 1──* Notes / Folders / Trash / Images / NoteSettings
```

No Organization, School, Classroom, Membership, Role, or Tenant tables exist.

---

## Important business constraints (application-level)

Verified in validators / stores:

- Max ~20,000 items per collection
- Max ~256 KB per study record JSON
- Notes document size limits (~1 MB) and image size limits (~8 MB)
- Settings numeric ranges (pomodoro, goals in 15-minute steps)
- Username uniqueness (case-insensitive normalized)

---

## Tenant / organization relationships

**Not present.** Isolation is strictly `UserId` ownership.

---

## Limitations affecting future development

1. **JSON document model** for study data makes SQL reporting, school aggregation, and leaderboards expensive/awkward.
2. **No migrations pipeline** increases production schema-change risk.
3. **Images in DB** as data URLs will hurt backup size and query performance at scale.
4. **No soft-delete / audit tables** for study records (except notes trash).
5. **No multi-workspace key** (`WorkspaceId` / `TenantId`) anywhere — required for school/personal dual mode.
6. **EnsureCreated** will not alter existing tables when model changes; production upgrades need an explicit migration strategy later.
7. **MARS / transaction savepoint warning** observed previously with SQL Server when MARS enabled — relevant for complex import transactions.

## Recommendation (audit only — do not implement yet)

When Phase 1 starts, introduce EF migrations and gradually **promote hot fields** (session duration, subject, dates, scores) into typed columns or satellite tables while keeping JSON for flexible extras — without rewriting the whole app at once.
