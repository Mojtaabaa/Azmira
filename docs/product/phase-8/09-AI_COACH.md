# 9. AI Learning Coach architecture

## Role
Explainable coach grounded in **actual learner data**. Never fabricate scores, sessions, or mastery.

## Data inputs (read-only tools)
- Mastery nodes + EMA history
- Due / overdue SRS
- Recent assessments & mistakes
- Planner load next 7 days
- Mission / journey progress
- Goals / konkoor date from settings
- Workspace role (student vs teacher context)

## Capabilities
| Capability | Behavior |
|------------|----------|
| Performance analysis | Summarize weak subjects with cited metrics |
| Planning recommendations | Suggest task list → existing resolve-to-tasks flow |
| Weakness detection | Rank nodes by evidence deficit |
| Explanation & feedback | “Why this review?” with card/topic refs |
| Interaction | Chat thread (Phase 12) + existing Analyze / Weekly / Suggest |

## Confidence & uncertainty
- Every recommendation carries `confidence: high|medium|low` and `evidenceRefs[]`
- Low confidence → ask clarifying question or defer (“داده کافی نیست”)
- No invented percentiles

## Privacy & permissions
- Process within tenant; no cross-workspace training dumps
- School: respect parental / admin AI flags
- User can disable coach; redact notes if marked private
- LLM provider optional; **heuristic path remains default** when no key (current behavior)

## Learner interaction model
1. Structured panels first (facts)
2. Optional conversational deepen
3. Actions always user-confirmed before mutating planner/quests

## Non-goals
- Autonomously changing grades or mastery
- Shadow surveillance of parents/teachers without role