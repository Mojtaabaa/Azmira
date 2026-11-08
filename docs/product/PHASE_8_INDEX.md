# Phase 8 — Product Experience Foundation & Signature Identity

**Status:** Complete (audit + architecture + design). Phase 9 implementation approved and in progress/complete — see `PHASE_9_REPORT.md`.

Rotbeh is framed as four equal pillars:

| Pillar | Intent |
|--------|--------|
| **PLAN** | Plan and manage learning |
| **LEARN** | Learn, practice, review, mastery |
| **IMPROVE** | Understand performance and improve |
| **ACHIEVE** | Missions, achievements, healthy celebration |

## Deliverables

| # | Document |
|---|----------|
| 1 | [Current-state audit](phase-8/01-CURRENT_STATE_AUDIT.md) |
| 2 | [Information architecture](phase-8/02-INFORMATION_ARCHITECTURE.md) |
| 3 | [Four-pillar journeys](phase-8/03-FOUR_PILLAR_JOURNEYS.md) |
| 4 | [Visual identity](phase-8/04-VISUAL_IDENTITY.md) |
| 5 | [Avatar domain](phase-8/05-AVATAR_DOMAIN.md) |
| 6 | [Learning Journey](phase-8/06-LEARNING_JOURNEY.md) |
| 7 | [Daily Missions](phase-8/07-DAILY_MISSIONS.md) |
| 8 | [Personal Improvement](phase-8/08-PERSONAL_IMPROVEMENT.md) |
| 9 | [AI Coach](phase-8/09-AI_COACH.md) |
| 10 | [Data/API impact](phase-8/10-DATA_API_IMPACT.md) |
| 11 | [Roadmap Phases 9–13](phase-8/11-ROADMAP_9_13.md) |
| 12 | [Acceptance & tests](phase-8/12-ACCEPTANCE_AND_TESTS.md) |

## Hard constraints (preserved)

- Hybrid / multi-tenant workspace model (`X-Rotbeh-Workspace`, Personal vs School).
- Existing auth, RBAC, platform/school admin gates, webhook SSRF/DNS pin.
- RTL (fa) + LTR readiness; Estedad / Vazirmatn.
- No Phase 8 schema migrations or production data changes.
- Incremental implementation only from Phase 9 onward.

## Product decisions (approved for Phase 9+)

1. Navigation: **Pillar Navigation + Functional Groups**
2. Avatar: **SVG/CSS first** (commissioned packs later via asset interface)
3. Journey: **Hybrid** (bootstrap from topics + authorable packs); engine from Phase 11
4. Personal Improvement: **Self-first** (optional school cohort later with privacy)
5. AI Coach: **Heuristic-first + LLM-ready** (no LLM required for core UX)