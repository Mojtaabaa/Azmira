# 8. Personal Improvement scoring specification

## Separate scores (never conflate)

| Score | Meaning | Primary signals |
|-------|---------|-----------------|
| **Absolute mastery** | Current competence | Mastery EMA, assessment scores |
| **Personal improvement** | Change vs own baseline | Δ mastery / Δ assessment vs baseline window |
| **Achievement activity** | Healthy engagement | Missions completed with evidence, badges earned |

## Baseline
- **Cold start:** first 14 days with ≥ N evidence events → baseline = mean of first window
- **Insufficient data:** show “در حال جمع‌آوری معیار پایه” — no rank / no peer compare
- **Rebaseline:** optional every semester or after long inactivity (≥ 30d) with user consent

## Normalization
- Per-subject z-like relative to personal history (not cohort)
- Cross-subject composite: weighted by study plan subjects, not global average of zeros
- Starting level differences handled by **percent improvement** and **evidence quality**, not raw score gaps

## What must not be rewarded
- Time-on-app, idle timer minutes, XP farming, login-only streaks as substitutes for learning
- Artificial activity spikes without correctness / mastery movement

## Surfaces
- IMPROVE hub: personal sparkline vs baseline
- ACHIEVE leaderboard: default **Personal Improvement** self view; optional school cohort if policy allows
- Existing `LearningImprovement` peer metric: keep as optional school view; label clearly as peer comparison

## Privacy
Default private. School cohort opt-in or admin-configured. Parents see child’s improvement narrative, not public ranks.