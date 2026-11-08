# Phase 14 — Beta Release Gate

**Date:** 2026-11-08  
**Branch:** `feat/phase-14-beta-release-gate`  
**Base:** `origin/main` @ `99e4e38` (PR #5 / Phase 13 merged)  
**Status:** Implemented for review — **no production deploy** in this phase.

## Why Phase 14

Roadmap Phases 9–13 are complete. Phase 14 closes the **Experience Beta release gate**: ops checklist, migration inventory, home compose as primary Home path, and release smoke tests.

## Delivered

| Item | Detail |
|------|--------|
| Checklist | `PHASE_14_BETA_CHECKLIST.md` |
| Roadmap | Extended `11-ROADMAP_9_13.md` with Phase 14 |
| Home | Dashboard prefers `GET /learner/home` (less fan-out) |
| Smoke tests | `Phase14ReleaseSmokeTests` — home contracts, privacy default, journey/improvement |
| Ops docs | Migration inventory pointer in `DATABASE_MIGRATIONS.md` |

## Explicitly not done

- Production deploy / live `database update`
- Rotating leaked credentials (ops action)
- Full axe CI pipeline

## Residual risks for go-live

1. Apply all pending migrations only after DB backup.  
2. Existing leaderboard rows with `LeaderboardVisible=true` remain visible; missing rows stay private (opt-in).  
3. Shared `wwwroot` static assets can race with concurrent FE build + `dotnet test` — clean between jobs.  
4. PKCS package advisories remain (upstream).

## Verdict

**Ready for Beta ops dry-run** (backup → migrate non-prod → smoke checklist). Not an automatic production ship.
