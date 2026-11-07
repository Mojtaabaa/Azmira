# ROTBEH Target Architecture

**Phase:** 1 — Design only (no implementation)  
**Basis:** `/docs/rotbeh-analysis/*` + live site `https://rotbeh.viannacloud.ir`  
**System of record:** ASP.NET Core (.NET 10) + React/Vite + SQL Server rewrite (`backend/`, `frontend/`)

## 1. Product stance

ROTBEH becomes a **hybrid personal + institutional learning platform**:

| Audience | Primary surface |
| --- | --- |
| Individual student | Personal Workspace (default; no school required) |
| School student | School Workspace membership |
| Teacher | School Workspace teaching tools |
| Parent | Linked read views (consent-based) |
| School / academic admins | School management console |
| Platform administrator | Cross-tenant operations |

One identity can hold **many workspace memberships**. Personal data stays private unless the user explicitly shares or joins a school context.

## 2. Architectural style (approved direction)

**Extend the existing modular monolith.** Do not introduce microservices in Phase 1–6 unless operational evidence demands it.

```
┌──────────────────────────────────────────────────────────┐
│ React SPA (fa/en, RTL/LTR)                               │
│  Personal shell │ School shell │ Parent shell │ Admin    │
└───────────────────────────┬──────────────────────────────┘
                            │ same-origin /api  (+ future API keys)
┌───────────────────────────▼──────────────────────────────┐
│ ASP.NET Core Modular Monolith (.NET 10)                  │
│  Identity │ Workspace │ Study │ Planner │ Assessment     │
│  Analytics │ Gamification │ Achievement │ Competition    │
│  Rewards │ AI │ School │ Notifications │ Subscription    │
│  Integration API                                         │
│  + Outbox / Domain Events processor                      │
└───────────────────────────┬──────────────────────────────┘
                            │ EF Core
┌───────────────────────────▼──────────────────────────────┐
│ SQL Server                                               │
│  Existing tables preserved + additive workspace columns  │
│  New module tables │ Outbox │ XP ledger │ RBAC           │
└──────────────────────────────────────────────────────────┘
         │
         └─ future: object storage for note images / exports
```

## 3. Core design decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Tenancy unit | **Workspace** (Personal or School-linked) | Matches product language; schools map 1:1 to a School Workspace |
| Isolation | Backend `WorkspaceId` on owned rows + membership checks | Never trust client-only filters |
| Study data | Keep JSON document collections initially; add `WorkspaceId` + projections | Least disruption to live personal product |
| AuthN | Keep cookie sessions; add API keys later for integrations | Compatible with current SPA |
| AuthZ | Role + resource policy on workspace membership | Required for school/parent |
| Gamification | Transactional outbox → idempotent processors | Prevents duplicate XP |
| Deploy | Single IIS/Kestrel site (current publish model) | Matches production |

## 4. Module map (logical, same deployable)

| Module | Reuses today | New |
| --- | --- | --- |
| Identity | Users, sessions, password hasher | Profile fields, platform roles |
| Workspace | — | Workspace, members, roles, settings, subscription |
| Study | `StudyRecords` collections, timer, topics | Workspace scoping, typed projections |
| Planner | events/tasks UI + API | Shared calendars, school schedules |
| Assessment | tests/mistakes personal flow | Assignments, question banks, grades |
| Analytics | personal charts | Cohort / school dashboards |
| Gamification | streak KPI only | Event rules, XP, levels |
| Achievement | — | Badges |
| Competition | — | Teams, leaderboards |
| Rewards | — | Store, inventory, avatar |
| AI | — | Coach conversations (opt-in) |
| School Management | — | Schools, invites, org settings |
| Notifications | browser notify helper | In-app inbox + channels |
| Subscription | — | WorkspaceSubscription plans |
| Integration API | account export/import | Public REST + webhooks |

## 5. Request pipeline (target)

1. Authenticate cookie / API key → `UserId`
2. Resolve **active workspace** from header `X-Rotbeh-Workspace` (or session default)
3. Authorize membership + role for route
4. Execute command/query **scoped by WorkspaceId**
5. Emit domain events to **outbox** in same DB transaction when state changes
6. Background processor applies gamification/notifications idempotently

## 6. Backward-compatibility risks (approval gate)

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Adding `WorkspaceId` to existing rows | Requires backfill of Personal workspace per user | Additive nullable → backfill → NOT NULL |
| Dual legacy Node tree | Feature drift | Freeze legacy; SoR = rewrite |
| `EnsureCreated` vs migrations | Production upgrade hazard | Adopt EF migrations before schema expand |
| Cookie CSRF / multi-origin school portals | Auth bugs | Keep same-site SPA; document proxy rules |
| Opaque JSON study docs | Weak school reporting | Additive projection tables, no forced rewrite |
| Images in SQL | Scale | Gradual blob migration |
| Gamification without idempotency | Duplicate XP | Outbox + unique event keys (mandatory) |
| Parent access | Privacy | Explicit guardian links + consent |

## 7. What this phase does **not** do

- No code, schema, or production config changes
- No data migration execution
- No UI implementation of school/parent shells yet

## 8. Approval checklist

- [ ] Confirm Workspace as tenancy boundary
- [ ] Confirm modular monolith (no microservices)
- [ ] Confirm preserve existing tables + additive migrations
- [ ] Confirm outbox-based gamification
- [ ] Confirm personal users remain school-optional
- [ ] Approve proceeding to implementation Phase 2 (Workspace foundation)

Companion docs in this folder detail each area.
