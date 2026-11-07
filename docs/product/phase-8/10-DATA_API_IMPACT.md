# 10. Database / API impact assessment

**Phase 8:** documentation only — **no migrations**.

## Reuse as-is (near term)
- `StudyRecords`, mastery, SRS, assessments
- Gamification: XP, badges, quests, streaks, outbox
- Store: items, inventory, `UserAvatarLoadout`
- AI: analyze / weekly / suggest endpoints

## Likely Phase 9–13 schema (when approved)

| Area | Change |
|------|--------|
| Avatar | `EquippedSlots` JSON on loadout; `UnlockRule` on store items |
| Journey | `JourneyDefinitions`, `Regions`, `Stages`, `LearnerJourneyProgress` |
| Missions | `MissionInstances` (user/day/category/evidence) |
| Improvement | `PersonalBaselines`, `ImprovementSnapshots` (or materialized views) |
| AI Coach | `CoachThreads`, `CoachMessages` (optional); audit log of evidence refs |

## API sketch (additive, versioned under existing auth + workspace header)

```
GET  /api/learner/home          # composed dashboard DTO
GET  /api/journey/me
POST /api/journey/evaluate-unlock
GET  /api/missions/daily
POST /api/missions/{id}/complete  # server verifies evidence
GET  /api/improvement/me
GET  /api/avatar/me
POST /api/avatar/equip
POST /api/coach/chat              # Phase 12
```

All routes: authenticated + `X-Rotbeh-Workspace` + existing tenant filters. No auth model changes.

## Risk
- Dual streak sources until Home uses server streak only
- Quest `manual` completion hole until fixed
- Large journey tables if content authoring precedes tooling