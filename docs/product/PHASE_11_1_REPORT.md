# Phase 11.1 — Final Integration & Release Gate

**Branch:** `feat/phase-11-learning-journey`  
**Base commit before Phase 11 package:** `6bf8fbd`  
**Status:** Release gates evaluated below. No merge / deploy / Phase 12.

## Audit summary

| Gate | Result |
|------|--------|
| Phase 11 change review + `PHASE_11_REPORT.md` | Pass |
| Migration `AddLearningJourney` Up/Down | Pass — additive tables; Down drops journey tables only (destructive to journey data, not other domains) |
| Bootstrap mastery/SRS | Pass |
| Unlock evidence-only; XP/time/streak alone rejected | Pass (`ContainsRejectedSignal` + `TryEvaluate` + tests) |
| Tenant/workspace isolation + idempotency | Pass — progress scoped by workspace/user/journey; outbox idempotency keys; graph load workspace-filtered |
| API `RequireWorkspace` on GET/POST journey | Pass |
| UI `/journey`, LEARN nav, states, evidence drawer | Pass (code review + FE tests/build) |
| `JourneyStageUnlocked` outbox + cosmetic hook | Pass (tests) |
| Compatibility with mastery/SRS/missions/achievements | Pass — additive APIs/events; no mission/XP path changes |
| Full backend / FE tests / production build | See verification |
| Diff hygiene (no secrets/wwwroot) | Pass (`wwwroot` gitignored) |

### Migration notes
- Creates: `JourneyDefinitions`, `JourneyRegions`, `JourneyStages`, `LearnerJourneyProgress`
- Unique indexes: `(WorkspaceId, Code)` filtered where WorkspaceId not null; `(WorkspaceId, UserId, JourneyId)`
- Cascade deletes within journey graph; does not alter mastery/SRS/missions tables
- Rollback (`Down`) removes journey tables and learner progress — safe for other domains, loses journey progress

### Hardening in 11.1
- `LoadJourneyGraphAsync` requires matching workspace (or system null workspace)
- Progress reload asserts workspace + user
- Stronger rejected-signal detection; added isolation + outbox/cosmetic tests

## Remaining risks
- Bootstrap journey is per-workspace code keyed by workspace, shaped by the calling user's mastery/SRS at first creation — later learners in same school workspace share that graph (authorable packs deferred)
- Cosmetic hook notifies via `GameNotification`; does not yet grant store inventory items
- Always-open first stages unlock without learning evidence (by design)
- EF InMemory CI cannot prove SQL unique-index races

## Release actions
Documented in final agent reply: commit SHA, push, PR URL or auth blocker.
