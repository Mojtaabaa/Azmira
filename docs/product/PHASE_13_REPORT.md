# Phase 13 — Polish, school packs, hardening

**Branch:** `feat/phase-13-polish-hardening`  
**Base:** `origin/main` @ `3b4bf6d` (PR #4 / Phase 12 merged)  
**Status:** Implemented locally for review — no deploy.

## Scope delivered

| Roadmap item | Delivery |
|--------------|----------|
| Authorable journey packs for schools | `POST/GET /journey/packs`, publish + activate; `IsPublished` migration |
| Motion / empty-state polish; a11y | EmptyState animation + `prefers-reduced-motion`; journey pulse + reduced-motion; focus-visible |
| Load tests on home compose | `GET /learner/home` + parallel load test (40×) |
| Privacy review | Documented below; home payload exposes privacy defaults |
| Deprecate duplicate streak UI | Home uses **server streak only** (`—` when unavailable) |

## Migration

`AddJourneyPackPublishFlags` — `JourneyDefinitions.IsPublished`, `PublishedAt`

## Privacy review (Beta)

- Leaderboards remain **opt-in** (Phase 12.1).
- Improvement remains **self-first**.
- Journey packs are **workspace-scoped**; activate rejects cross-workspace pack ids.
- Coach evidenceRefs remain server-derived (Phase 12.1).
- Home compose does not expand peer ranks.

## A11y notes

- Empty states: `role="status"`, `aria-live="polite"`, focus-visible outline.
- Motion gated by `prefers-reduced-motion`.

## Out of scope

- Full E2E a11y audit tooling (axe CI) — manual checklist only this phase.
- Deploy / production migrate.
