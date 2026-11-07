# Implementation Recommendations (No Code Changes Yet)

## Guiding principles

1. Extend the **rewrite monolith** (`backend` + `frontend`); do not invent microservices.
2. Stop investing in the **legacy Node/SQLite** path except for migration/parity fixes.
3. Introduce **Workspace + migrations** before school or gamification features that need shared data.
4. Preserve existing personal UX; school features should be additive shells/modules.
5. Wait for human approval before any architecture or schema change.

## Recommended target architecture

Modular ASP.NET Core monolith + React SPA, SQL Server, workspace-scoped data, later object storage for images, later public API façade.

Details: see `ARCHITECTURE_ASSESSMENT.md`.

## Required database modifications (future — not in Phase 0)

Minimum foundation:

1. Adopt **EF Core migrations** (replace reliance on `EnsureCreated` for upgrades).
2. Add `Workspaces` (`Id`, `Type` Personal|School, `OwnerUserId` / `SchoolId`, timestamps).
3. Add `WorkspaceMembers` (`WorkspaceId`, `UserId`, `Role`).
4. Add `WorkspaceId` to study ownership (either new columns on `StudyRecords` / notes tables or a cutover table).
5. Optional promotion of hot JSON fields: `SubjectId`, `StartedAt`, `DurationMinutes`, `Score` for analytics/leaderboards.
6. Gamification tables only after study event contracts are stable.
7. Move `NoteImages.DataUrl` toward blob/object storage references.

## Recommended implementation sequence

| Phase | Focus | Complexity | Depends on |
| --- | --- | --- | --- |
| **0** | Audit (this docs set) | S | — |
| **1** | Codebase consolidation: rewrite as SoR; archive/ignore legacy for features; CI smoke | M | 0 |
| **2** | EF migrations + Workspace foundation (every user gets Personal workspace) | M | 1 |
| **3** | Harden authZ helpers (resource access by workspace membership) | M | 2 |
| **4** | Promote study metrics columns / projections for analytics performance | M | 2 |
| **5** | Gamification MVP: events → XP → streak shields → badges | L | 3–4 |
| **6** | School entity + admin + member invites | L | 3 |
| **7** | Teacher portal + assignments/assessment loop | XL | 6 |
| **8** | Leaderboards / team competition (workspace scoped) | L | 5–6 |
| **9** | Parent portal (read-only + consent) | L | 6–7 |
| **10** | Public API keys + webhooks | L | 3–4 |
| **11** | AI coach (opt-in) | XL | 4 + privacy review |

Complexity key: S small, M medium, L large, XL extra-large.

## Features to reuse

- Timer / Pomodoro
- Personal planner and tasks
- Tests + mistakes workflow
- Notes vault (rewrite SQL-backed)
- Personal analytics UI and weekly poster
- Account import/export pipeline

## Features to extend

- Auth session → workspace-aware claims
- Study JSON store → workspace keyed + typed projections
- Streak KPI → gamification input
- Analytics page → cohort mode when teacher role exists
- Data transfer → workspace-scoped backups

## Features needing new implementation

- Multi-tenant school model
- RBAC and portals (teacher/parent/admin)
- XP/levels/badges/quests/store
- Team competitions
- Public API + webhooks
- AI coach

## Critical technical risks (priority order)

1. Dual codebase drift (legacy + rewrite)
2. No migration strategy for SQL schema
3. JSON document model blocking school-scale queries
4. Images in database
5. Cookie-only API limiting integrations
6. In-memory rate limits under multi-instance hosting
7. Missing automated regression suite for rewrite
8. Production URL currently unverifiable (404)

## Phase 0 exit criteria

Human reviewers should confirm:

- [ ] Rewrite (`backend`/`frontend`) is the system of record going forward
- [ ] Legacy stack freeze policy
- [ ] Approval to start Phase 1 (tooling/consolidation) and Phase 2 (Workspace + migrations)
- [ ] Clarification of live deployment target (the provided URL returned 404 during audit)

**No application features should be implemented until that approval.**
