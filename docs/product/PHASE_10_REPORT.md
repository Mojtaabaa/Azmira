# Phase 10 — Daily Missions + Stage A/B

## Stage A — Merge Phase 7.2 into main

| Item | Result |
|------|--------|
| PR #1 | **Merged** (closed) |
| Base / head | `main` ← `chore/phase-7-2-release-gate` |
| Phase 7.2 commit | `6e72dd953f81e7d7d4c3d4fe3bf4335c8386fe1d` |
| Merge commit | `2cb75acc3c4cfc4e4012fd293499c6fdd47ea1cc` |
| `origin/main` | Contains Phase 7.2 as ancestor |
| Branch protection | None (`protected=false`, required checks empty) |
| Cursor suite | Still queued historically; **not required** |

No deploy. Source branch not deleted.

## Stage B — Preserve Phase 9

| Item | Result |
|------|--------|
| Branch | `feat/phase-10-daily-missions` |
| HEAD at branch create | `6e72dd9` (same tip as release-gate; dirty tree preserved) |
| Phase 9 files | Intact (`PHASE_9_REPORT.md`, avatar, pillars, learner home, etc.) |
| Uncommitted RBAC + Phase 8/9 | Carried forward (not discarded) |

No push / PR for Phase 9 or 10.

## Stage C — Daily Missions architecture

### Lifecycle (reuse `QuestDefinition` + `UserQuestProgress`)
- States: Active → Completed → Claimed; Expired; Cancelled untouched
- Daily instances: per-user `QuestKind.Personalized`, `Category=dm`, meta `[dm:{template}:{yyyy-MM-dd}]` in Description
- Rollover: local calendar day via workspace timezone (`MissionClock`)
- Idempotent claim (no second `QuestCompleted` outbox)
- Evidence sync: `GET/POST /missions/daily` (+ `/sync`)
- **No new DB migration** — deferred MissionInstance table to later if growth warrants

### Evidence & manual policy
- Progress from domain events / outbox evidence only: reviews, assessments, mastery, sessions, study minutes, daily goal
- `manual` **never** advances from events
- Teacher confirm: `POST /teacher/missions/{id}/confirm` → `QuestEngine.ConfirmManualAsync`
- Teacher-created missions use `QuestKind.TeacherAssigned` + `manual`

### Timezone
- Assignment keyed by local date in workspace TZ at issue time
- UTC `StartsAt`/`EndsAt` fixed for that day; TZ change does **not** rewrite issued missions
- DST via `TimeZoneInfo`

### Rewards & limits
- Max 3 missions/day; XP/coin caps on daily mission rewards
- Avoid repeating yesterday’s templates when alternatives exist
- Claim → existing XP/coin outbox path (server authoritative)
- Missed days: expire quietly, no punishment

### Frontend
- `/missions` Daily Missions page (ACHIEVE)
- Home ACHIEVE strip from `/missions/daily`
- Nav + command palette updated; Phase 9 AvatarPortrait retained

## Tests
- `DailyMissionTests`: 8 passed
- Frontend `npm test` + `npm run build`: see completion run

## Gaps / Phase 11
- Optional `MissionInstance` table if quest-row growth becomes an issue
- Learning Journey map engine
- Richer task-completion evidence event (planner tasks)
- Broader FE RTL/LTR visual QA

**Awaiting approval before Phase 11 / push.**
