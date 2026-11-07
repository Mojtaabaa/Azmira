# Tenant Isolation Design

## 1. Tenant definition

In ROTBEH, the **tenant boundary is the Workspace**.

| Workspace type | Tenant semantics |
| --- | --- |
| Personal | Private tenant of one owner; no school coupling |
| School | Institutional tenant; all school resources scoped to that workspace / school |

`SchoolId` is an organizational record. Runtime authorization and queries key off **`WorkspaceId`**.

Platform Administrators operate outside tenant bounds via explicit elevation (audited).

## 2. Tenant identification

| Layer | Mechanism |
| --- | --- |
| Browser | Active workspace id in client state + header `X-Rotbeh-Workspace` |
| API | Resolved `WorkspaceContext { WorkspaceId, Type, MemberRole, UserId }` |
| DB rows | Non-null `WorkspaceId` on tenant-owned tables after migration cutover |
| Integrations | API key bound to a workspace (future) |

Never accept `WorkspaceId` from body alone without membership verification.

## 3. Data ownership

Every tenant-owned entity must include:

- `WorkspaceId` (tenant)
- `CreatedByUserId` / `UserId` when person-scoped inside tenant
- Soft-delete columns where recovery matters

Global tables (no WorkspaceId): `Users`, platform catalogs, plan definitions, badge catalogs (optionally cloned per tenant ruleset).

## 4. Tenant-aware queries (mandatory pattern)

All store methods take `WorkspaceContext` (or `workspaceId` validated already):

```csharp
// Conceptual — design only
db.StudyRecords.Where(r => r.WorkspaceId == ctx.WorkspaceId && r.UserId == ctx.UserId)
```

School teacher queries may broaden to class roster **within same WorkspaceId** after permission check — never across workspaces.

## 5. Access-control enforcement points

1. **Middleware/filter:** build `WorkspaceContext` or fail.
2. **Authorization policies:** role requirements per endpoint.
3. **Resource guards:** load resource → compare `resource.WorkspaceId` to context → deny on mismatch.
4. **Query filters (optional EF global filters):** `HasQueryFilter(e => e.WorkspaceId == currentWorkspaceId)` for defense-in-depth; still keep explicit checks for elevated roles.

Cross-tenant IDOR attempts must return **404** (not 403) for non-admin callers when resource exists in another tenant, to reduce enumeration — except where UX needs explicit forbidden.

## 6. Cross-tenant security rules

| Rule | Enforcement |
| --- | --- |
| No joins across WorkspaceId in app queries | Code review + helpers |
| Export/import scoped to active workspace | Account export evolves to workspace export |
| Parent sees only linked students | GuardianStudent table |
| Teacher sees only assigned classes/students | ClassMembership |
| Platform admin actions audited | AuditLog |
| Background jobs carry WorkspaceId in payload | Outbox event envelope |

## 7. Tenant-specific configuration

Stored in `WorkspaceSettings` JSON + typed columns:

- Locale, timezone, feature flags
- Study defaults (pomodoro, goals)
- Privacy (leaderboard visibility)
- Assessment policies
- Notification preferences

School branding/policies may also live on `Schools` and be read through the school workspace.

## 8. Tenant-specific gamification rules

| Concept | Storage |
| --- | --- |
| Rule profile | `GamificationProfiles` (global template or workspace-owned) |
| Binding | `WorkspaceSettings.GamificationProfileId` |
| XP awards | `XpLedger` keyed by WorkspaceId + UserId |
| Idempotency | Unique `(WorkspaceId, EventId)` / `(WorkspaceId, IdempotencyKey)` |

A Personal workspace uses the platform default profile. A School may override multipliers, disabled event types, or badge sets without affecting other schools.

## 9. Testing requirements (for later implementation)

- User A cannot read User B personal workspace data
- School1 teacher cannot read School2 students
- Parent without link cannot read student analytics
- Missing/forged workspace header cannot escalate
- Event replay does not double-award XP across retries

## 10. Backward compatibility

Until clients send workspace headers, default Personal workspace preserves today’s isolation model (user-owned rows). Backend still backfills `WorkspaceId` so enforcement can become uniform.
