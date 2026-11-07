# ROTBEH (رتبه) — Project Overview

**Audit date:** 2026-11-07  
**Repository path:** `C:\Projects\rotbeh\konkoor-help`  
**Scope:** Source-code audit only. No code changes, no migrations, no production config changes.  
**Live URL provided:** `https://totbeh.viannacloud.ir`

## Verified summary

ROTBEH is a **personal konkoor (university entrance exam) study workspace**. The product helps an individual student track study sessions, tests, mistakes, tasks, planner events, topics, notes, and analytics.

The repository currently contains **two coexisting implementations**:

| Layer | Location | Status (from repo) |
| --- | --- | --- |
| Legacy monolith | root `src/` + `server/` | Node.js + Express + React (JS) + SQLite |
| Rewrite target | `frontend/` + `backend/` | React (TypeScript) + ASP.NET Core (.NET 10) + SQL Server |
| Server package | `publish/` + `publish.ps1` | Release build of rewrite (API + static SPA) |

**Verified:** Both stacks implement the same core product concept (auth + document-style study collections + notes).  
**Assumption:** The intended forward path is the `backend` + `frontend` rewrite, because `publish.ps1` builds that pair for server install.  
**Unable to verify:** Which stack (if any) currently runs at `https://totbeh.viannacloud.ir`. A browser and HTTP fetch against that URL returned **HTTP 404 Not Found** during this audit.

## Product character (verified from UI routes and data model)

- Single-user accounts (username + password)
- Per-user private data (sessions, tests, mistakes, topics, tasks, events, settings)
- Focus / Pomodoro timer with planner sync
- Test capture and mistake review
- Weekly analytics and PNG weekly report export
- Markdown notes vault (folders, tags, wikilinks, graph, images)
- Account backup import/export (rewrite stack)

## What this product is not (verified absent in code)

- Schools / organizations / multi-tenant workspaces
- Teacher, parent, or admin portals
- Roles and permissions beyond “authenticated owner of own data”
- Gamification (XP, badges, quests, leaderboards, store)
- Public API / webhooks for third parties
- Background job workers, cache/message buses

## Dual-codebase risk

Keeping both legacy and rewrite trees increases drift risk. Feature parity is high for study collections, but notes storage and account transfer differ:

- Legacy notes: browser IndexedDB (Dexie)
- Rewrite notes: SQL Server tables synced over `/api/notes/*`

## Audit deliverables in this folder

1. `PROJECT_OVERVIEW.md` (this file)
2. `TECHNOLOGY_STACK.md`
3. `REPOSITORY_STRUCTURE.md`
4. `EXISTING_FEATURES.md`
5. `DATABASE_ANALYSIS.md`
6. `SECURITY_AUDIT.md`
7. `ARCHITECTURE_ASSESSMENT.md`
8. `FEATURE_GAP_ANALYSIS.md`
9. `IMPLEMENTATION_RECOMMENDATIONS.md`

## Final stance for Phase 0

This phase is **read-only**. No architecture replacement and no feature implementation should proceed until human review of these documents.
