# 11. Implementation roadmap — Phases 9–13

## Phase 9 — Experience shell & identity
- Pillar navigation + learner home composition (consume existing APIs)
- Design token unification; EmptyState / Meter / PillarSection
- AvatarPortrait prototype (CSS/SVG layers) on Progress/Store — **no migration required if using current loadout**
- Acceptance: Home shows four pillars; RTL/LTR; no auth regressions

## Phase 10 — Daily Missions & quest fixes
- Daily rollover / MissionInstance (migration)
- Category generator + caps + safeguards
- Fix `manual` quest progression / teacher confirm
- Wire Home missions strip

## Phase 11 — Learning Journey
- Journey schema + unlock service from mastery/SRS/assessments
- Map UI + evidence drawer
- Bootstrap from topics/mastery
- Achievement-linked cosmetics hooks

## Phase 12 — Personal Improvement + AI Coach v2
- Baselines + improvement API + IMPROVE hub charts
- Leaderboard labeling / privacy defaults
- Coach chat with evidenceRefs + confidence; keep heuristic fallback

## Phase 13 — Polish, school packs, hardening
- Authorable journey packs for schools
- Motion / empty-state polish; a11y audit
- Load tests on home compose endpoint; privacy review
- Deprecate duplicate streak UI

Each phase: incremental PR, tests, no drive-by refactors.