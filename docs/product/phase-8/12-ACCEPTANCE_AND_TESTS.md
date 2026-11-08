# 12. Acceptance criteria & test strategy

## Phase 8 acceptance (this phase)
- [x] Audit grounded in repository inspection
- [x] Four-pillar IA documented
- [x] Journeys, visual identity, avatar, journey, missions, improvement, AI coach specified
- [x] Data/API impact without migrations
- [x] Roadmap 9–13 + open decisions listed
- [x] No auth/tenant/security control changes
- [x] No push / merge / deploy required for Phase 8 docs

## Phase 9+ acceptance themes
| Theme | Criteria |
|-------|----------|
| Pillars visible | Nav + Home expose PLAN/LEARN/IMPROVE/ACHIEVE |
| Evidence unlocks | Journey/missions reject time/XP-only completion |
| Avatar ethics | Mastery cosmetics non-purchasable |
| Improvement fairness | Insufficient data → no false rank |
| AI grounding | Responses include evidence refs or explicit uncertainty |
| Tenancy | Workspace isolation tests pass |
| i18n | RTL fa + LTR smoke |

## Test strategy
- **Unit:** unlock evaluators, mission generator caps, baseline math
- **Integration:** API home compose, mission complete with forged evidence rejected
- **Frontend:** RTL layout snapshots for Home / AvatarPortrait
- **Security:** existing PlatformRbac / school isolation suites remain green
- **No E2E compulsion:** assert daily reward caps

## Risks & unresolved decisions
See [PHASE_8_INDEX.md](../PHASE_8_INDEX.md) open decisions. Additional risks: content authoring cost; dual CSS systems; CI/merge of prior release PRs independent of Phase 8.