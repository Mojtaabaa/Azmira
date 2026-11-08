# Phase 11 — Learning Journey

**Branch:** `feat/phase-11-learning-journey`  
**Base:** Phase 10 tip (`feat/phase-10-daily-missions` / `6bf8fbd`)  
**Status:** Implemented; awaiting review — no merge/deploy/Phase 12 unless approved.

## Delivered

### Schema (migration `AddLearningJourney`)
- `JourneyDefinitions`, `JourneyRegions`, `JourneyStages`, `LearnerJourneyProgress`
- Unique `(WorkspaceId, Code)` and `(WorkspaceId, UserId, JourneyId)`

### Engine
- `JourneyService`: bootstrap from mastery subjects/topics (fallback SRS / «عمومی»)
- Evidence unlocks: mastery level/EMA, assessment %, successful SRS reviews
- **Rejected:** XP / study minutes / login streak alone (`ContainsRejectedSignal`)
- Outbox event `JourneyStageUnlocked`
- Achievement cosmetic hooks via `rewards.cosmeticKey` + notification payload

### API
- `GET /api/journey/me`
- `POST /api/journey/evaluate-unlock`

### Frontend (LEARN)
- `/journey` map UI with region themes, node states (locked/available/complete)
- Evidence drawer «چرا باز شد؟»
- Nav link under LEARN pillar

## Tests / verification
- `LearningJourneyTests`: **4 passed** (bootstrap, mastery unlock, XP-only rejection)
- Full backend suite: **99 passed**
- Frontend tests: **11 passed** (+ `/journey` nav assertion)
- Frontend production build: **OK**

## Out of scope / next
- Authorable school journey packs (Phase 13 packs)
- Rich SVG cartography / motion polish
- Phase 12 Personal Improvement + AI Coach v2

## Stop
No push/PR/merge/deploy until you ask. Phase 12 not started.
