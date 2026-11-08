# Phase 12 — Personal Improvement + AI Coach v2

**Date:** 2026-11-08  
**Branch:** `feat/phase-12-improvement-coach` (from Phase 11 tip `3ed3ccc`)  
**Status:** Implemented locally — **no commit / push / merge / deploy / Phase 13** without separate release-gate approval.

## Prerequisite check

| Item | Result |
|------|--------|
| Phase 11 PR #3 merged into `main`? | **No** — `origin/main` still at `2cb75ac` (Phase 7.2). PR #3 head = `3ed3ccc`. |
| Phase 12 base | Branched from Phase 11 tip to preserve Journey work |

## Scope (from `phase-8/11-ROADMAP_9_13.md`)

1. Baselines + improvement API + IMPROVE hub charts  
2. Leaderboard labeling / privacy defaults  
3. Coach chat with `evidenceRefs` + `confidence`; keep heuristic fallback  

Approved decisions preserved: **self-first** improvement; **heuristic-first + LLM-ready** coach; four-pillar nav; tenant isolation; additive APIs.

## Delivered

### Data
- `PersonalBaselines`, `ImprovementSnapshots`, `CoachThreads`, `CoachMessages`
- Migration: `20261108054311_AddPersonalImprovementAndCoach`

### API
- `GET /api/improvement/me` — cold-start Collecting / Ready, composite + per-subject, sparkline
- `POST /api/improvement/rebaseline` — requires `consent: true`
- `POST /api/coach/chat` — thread persistence; `reply`, `confidence`, `evidenceRefs`; heuristic fallback if provider fails

### Leaderboards / privacy
- New privacy rows default `LeaderboardVisible = false`
- Board payload includes `viewKind`, `labelFa`/`labelEn`, `peerComparison`, `privacyDefault`, `note`
- `LearningImprovement` explicitly labeled as optional peer comparison

### UI
- IMPROVE hub → personal improvement charts (`PersonalImprovementPage`)
- Route `/improvement`; nav link under IMPROVE
- Leaderboards default tab = personal self view; peer metric opt-in
- AI Coach chat panel with confidence + evidence refs

### Tests
- `backend.Tests/PersonalImprovementTests.cs` — cold start, baseline Δ, XP not rewarded, tenant isolation, privacy default, chat evidence/confidence, heuristic fallback, rebaseline consent
- Regression filters: LearningJourney, LearningIntelligence, SocialGamification
- `frontend/src/navigation/pillars.test.js` — `/improvement` route

## Out of scope (unchanged)

- Phase 13 polish / school journey packs / load tests  
- Deploy / production migrate  
- Silent product decision changes  

## Known risks

- Historical mastery EMA is not time-series; sparkline materializes weekly snapshots on read after Ready.
- Existing `UserPrivacySettings` rows keep prior `LeaderboardVisible` values; only new EnsurePrivacy defaults are private.
- Phase 11 still unmerged; Phase 12 must land after / with Phase 11.
- Coach chat requires workspace AI enabled (same gate as analyze/weekly).
