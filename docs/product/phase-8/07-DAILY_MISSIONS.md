# 7. Daily Missions domain model

## Categories

| Category | Examples | Evidence |
|----------|----------|----------|
| Study planning | Add 3 planner tasks for tomorrow | Tasks created |
| Learning new material | Complete topic section / assessment | Assessment / mastery delta |
| Practice | Timed practice set | Test/assessment attempt quality |
| Spaced repetition | Clear N due reviews | SRS reviews (correct ≥ floor) |
| Weakness improvement | Focus weak subject cards | Reviews/mistakes closed |
| Reflection | Self-rate confidence + note | Reflection record |

## Generation inputs
- Due reviews count, weak mastery nodes, planner emptiness, recent completion fatigue
- Workspace type (Personal vs School), age policy if present
- Active teacher assignments (school)
- Cap: **3 daily missions** default; hard max 5

## Difficulty
`Easy | Standard | Stretch` from learner recent success rate — stretch never mandatory for streak.

## Completion evidence
Must bind to domain events / IDs (session, review batch, assessment).  
`QuestEngine` today: `manual` never progresses — **fix in Phase 10** for teacher missions by requiring explicit evidence or teacher confirm API.

## Reward policy
- Coins / XP / badge progress **capped daily**
- Cosmetics only via achievement unlock rules, not daily spam
- No punishment for missed dailies (no streak break from missions alone)

## Safeguards
- No duplicate category same day unless insufficient alternatives
- Cooldown after N consecutive identical templates
- Quiet hours / reduced mission mode (settings)
- Anti-compulsion: hide “complete all for mega XP” dark patterns
- Daily rollover: regenerate at workspace-local midnight; expire incomplete without shame UX

## Relation to existing quests
Extend `QuestKind.Daily` definitions + add `MissionInstance` table (Phase 10) for per-user per-day state, or encode instances in progress JSON until migration approved.