# Architecture Assessment

## Current architecture (verified)

```
Browser (React SPA)
   │  cookie session + JSON REST
   ▼
ASP.NET Core Minimal API  (.NET 10)
   │  EF Core
   ▼
SQL Server
```

Alternate still in tree:

```
Browser (React JS) ↔ Express ↔ SQLite
(+ notes in IndexedDB)
```

### Style

- **Modular monolith**, not microservices
- **Document-oriented persistence** for study domain (JSON in rows)
- **SPA + BFF-less API** (same origin in production publish)

### Separation of concerns

| Layer | Quality |
| --- | --- |
| API endpoints vs stores | Reasonable |
| Domain validation | Centralized for settings/collections; weak for deep record schemas |
| Frontend feature folders | Good for notes; other domains are page/component oriented |
| Shared contracts | Informal JSON shapes; no OpenAPI |

## Fit for target product

Target needs personal + school workspaces, gamification, portals, public API. Current system is an excellent **Personal Workspace / Study Management** core, but lacks tenancy, RBAC, and event/game engines.

## Least-disruptive extension strategy (recommended)

**Keep the modular monolith.** Do **not** split into microservices unless proven operational need.

Proposed evolution path:

1. **Freeze legacy Node stack** for new features; treat rewrite as system of record.
2. Introduce **Workspace** as first-class boundary (`Personal` default workspace per user; later `School`).
3. Keep current JSON collections working under `WorkspaceId` to avoid big-bang rewrite.
4. Extract **read models / typed tables** only where analytics, leaderboards, or school reporting require them.
5. Add **module folders** inside the same API (`Workspaces`, `Schools`, `Gamification`, `Integrations`) rather than new deployables.
6. Publish **versioned public API** later as a thin façade over internal services.

## Why not microservices now

- Team/codebase size is a single product with shared transactional data (XP, study events, school memberships).
- Operational complexity (auth, deploy, tracing) would exceed current benefits.
- Existing publish model is one IIS/Kestrel site.

## Compatibility with “extend, don’t replace”

Compatible: timer, planner, tests, mistakes, analytics UI, notes, account backup can remain.  
Incompatible without schema extension: school isolation, multi-workspace, RBAC, leaderboards at SQL scale, webhooks.

## Architectural risks

1. Dual codebase drift (`src/` vs `frontend/`, `server/` vs `backend/`)
2. `EnsureCreated` vs real migrations
3. Growing JSON blobs blocking school BI
4. Session/cookie model may need API tokens for public API later (additive)

## Target architecture sketch (recommendation only)

```
React SPA (personal + school shells)
        │
ASP.NET Modular Monolith
  Auth / Sessions
  Workspaces
  Study (existing collections + typed projections)
  Notes
  Gamification
  Schools / Memberships / RBAC
  Integrations (webhooks outbound)
        │
SQL Server (tenant/workspace keyed tables)
Object storage (images/files)  [future]
```

This preserves today’s deploy shape while enabling the future product.
