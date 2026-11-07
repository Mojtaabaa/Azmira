# 1. Current-state audit

Inspected: `frontend/` (Layout, App routes, design tokens, pillar-mapped pages) and `backend/` (entities, services, APIs). Capabilities were **verified in code**, not assumed missing.

## What already exists (reuse)

### Navigation & shell
- Sidebar groups: امروز / تمرین / برنامه / حساب — [`Layout.tsx`](../../frontend/src/components/Layout.tsx)
- Capability-gated school/platform links — [`capabilities.js`](../../frontend/src/utils/capabilities.js)
- Workspace switcher, theme toggle, command palette, mini timer, gamification toasts

### PLAN surfaces
- Home dashboard widgets, planner, tasks, notes, settings (goals / konkoor date)
- Study data: `StudyRecords` collections (`sessions`, `tasks`, `events`, …)

### LEARN surfaces
- Timer, tests, topics, assessments, notes
- Mastery nodes + EMA evidence — `MasteryService`
- Spaced repetition cards/reviews — `SpacedRepetitionService`

### IMPROVE surfaces
- Mistakes, reviews, mastery map (flat list), analytics charts, AI coach analyze/report/suggest
- Leaderboard metric `LearningImprovement` (workspace peers, test-score delta by subject)

### ACHIEVE surfaces
- Progress (XP, level, badges, quests, streak), store, leaderboards, teams, competitions
- Store categories include `AvatarBase`, `Accessory`, `Clothing`, `Background`, `Frame`
- `UserAvatarLoadout` + inventory APIs under `/store`

### Tenancy / school
- Personal vs School workspaces; teacher/parent/school admin portals; platform admin + school requests

## Design system inventory
- Global tokens: [`index.css`](../../frontend/src/index.css) (accent terracotta `#c45c26` / dark `#e8924a`, Estedad+Vazirmatn)
- Parallel “mini systems”: Progress / Social / LearningIntelligence / SchoolEdition CSS families
- Gap: `--panel` referenced but not defined; inconsistent empty states; command palette missing many routes

## Gaps (confirmed)

| Desired | Today | Gap |
|---------|-------|-----|
| Four-pillar IA | Implicit page mapping | No explicit PLAN/LEARN/IMPROVE/ACHIEVE nav or home composition |
| Signature identity | Strong accent + fonts | Fragmented cards/heroes; school hex colors vs tokens |
| Avatar experience | Store loadout IDs + CSS classes | No unified profile DTO; no composed Avatar component; cosmetics not tied to mastery evidence |
| Learning Journey map | Flat `/mastery` list | No regions/stages/unlock graph |
| Daily Missions | Seeded `QuestKind.Daily` | No daily rollover/respawn; weak personalization; `manual` teacher missions never progress in `QuestEngine` |
| Personal Improvement | Peer `LearningImprovement` board | No self-baseline time series; ignores assessment/mastery evidence |
| AI Coach | Analyze / weekly / planner suggest | No chat thread; heuristic default; no grounded tool loop |

## Duplications / inconsistencies
- Local study streak (dashboard) vs server gamification streak (Progress)
- `.meter` vs `.progress-bar` vs widget bars
- `/admin/gamification` not in nav (only Progress link)
- Nav icon reuse (many map to `chart`)

## Integration points for Phase 9+
- Extend `QuestDefinition` / outbox pipeline for daily missions
- Extend `UserAvatarLoadout` + store unlock rules (non-purchasable mastery cosmetics)
- New journey tables *later* (Phase 9+ migrations only)
- Compose learner home from existing `/gamification/me`, `/mastery`, `/reviews/due`, `/data` sessions
- Unify design tokens; introduce shared `EmptyState` / `PillarSection` components without ripping pages