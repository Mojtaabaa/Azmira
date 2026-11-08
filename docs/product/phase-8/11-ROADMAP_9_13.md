# 11. Implementation roadmap — Phases 9–16

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

## Phase 14 — Beta release gate
- Experience Beta readiness checklist (Phases 8–13)
- Migration inventory for ops (no production deploy in-phase)
- Home prefers `/learner/home` compose; reduce redundant fan-out
- Release smoke tests for critical learner paths
- Residual risk register for go-live

## Phase 15 — Production go-live
- Merge Phase 14; SQL backup gate before any IIS overwrite
- Publish Release package (`publish.ps1`); deploy to https://rotbeh.viannacloud.ir
- Startup `MigrateAsync` for pending Experience Beta migrations
- Post-deploy health + product/privacy smoke; go-live report
- Rollback = prior binaries + DB restore (no hand-edited schema)

## Phase 16 — Learner UX & KPI completion
- Home compose-first (pulses, merge PLAN, mission meters; drop `/mastery` fan-out)
- Learn/Achieve hubs with live KPIs + primary CTA
- Progress / Daily Missions management (filters, deep-links, gated admin)
- Journey % KPIs + unlock checklist; Improvement sparkline polish
- Coach structured cards; Leaderboards subject picker; Store browse filters

Each phase: incremental PR, tests, no drive-by refactors.
