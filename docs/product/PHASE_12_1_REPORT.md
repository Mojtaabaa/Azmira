# Phase 12.1 — Final Integration Audit

**Date:** 2026-11-08  
**Branch tip (committed):** `3ed3ccc` on `feat/phase-12-improvement-coach`  
**Working tree:** Phase 12 + 12.1 audit fixes **uncommitted**  
**Constraints honored:** no commit / push / merge / deploy / Phase 13

## 1. Branch dependency graph

```
origin/main ──────────────────────── 2cb75ac  (PR #1 / Phase 7.2)
                                         │
                                         │  4 commits ahead on feature lines
                                         ▼
                          0ed67fd  Phase 9–10 Daily Missions
                          6bf8fbd  Phase 10.3 docs
                          a09f743  Phase 11 Learning Journey
                          3ed3ccc  Phase 11.1 release-gate docs  ← HEAD of both
                                         │                      feat/phase-11-learning-journey
                                         │                      and feat/phase-12-improvement-coach
                                         ▼
                          [uncommitted] Phase 12 + 12.1 fixes
```

| Question | Answer |
|----------|--------|
| Merge-base with `origin/main` | `2cb75ac` |
| Commits `origin/main..HEAD` | 4 (Phases 9–11.1) |
| Would a Phase 12 PR include Phase 11? | **Yes** — Phase 12 branch starts at Phase 11 tip; Phase 12 code is still only in the working tree |
| PR #3 merged? | **No** |

### Recommended PR sequence

1. **Merge PR #3** (Phase 11 / `feat/phase-11-learning-journey` → `main`) first.  
2. Rebase or open Phase 12 PR **from updated main** (or stack Phase 12 on #3) so review diff is Phase-12-only.  
3. Do **not** merge a single PR that mixes Phase 11 + 12 unless release intentionally wants a stacked ship — then label it clearly as Phases 11–12.

Safest for Beta: **#3 → Phase 12 PR → (later) Phase 13**.

## 2. Tenant isolation & data correctness

| Check | Result |
|-------|--------|
| Improvement queries scoped by `WorkspaceId` + `UserId` | Pass |
| Baseline create / Collecting→Ready / weekly snapshot | Pass |
| XP / time not used as evidence | Pass |
| Empty / Collecting / Ready states | Pass |
| Chart honesty | **Fixed in 12.1** — API `sparklineSource=weeklySnapshotsOnRead` + UI note (not reconstructed mastery history) |

## 3. Leaderboard privacy

| Check | Result |
|-------|--------|
| New privacy rows default `LeaderboardVisible=false` | Pass |
| **Members with no privacy row** | **Was HIGH:** still appeared on boards (opt-out). **Fixed 12.1:** opt-in only (`LeaderboardVisible=true`) |
| Existing rows | **No data migration required** — `true` stays visible, `false` stays hidden; missing row now means private |
| `total` / entries leak when private | Pass after opt-in fix |
| Name default when settings missing | Tightened to `ShowRealName ?? false` |

## 4. AI Coach

| Check | Result |
|-------|--------|
| Thread load scoped to workspace+user | Pass |
| Cross-workspace thread id reuse | Rejected (tested) |
| Provider-fabricated `evidenceRefs` | **Was HIGH:** accepted blindly. **Fixed 12.1:** server facts only; `evidencePolicy=serverFactsOnly` |
| Confidence inflation by provider | **Fixed:** can only lower vs server |
| Heuristic fallback / AI disabled | Pass (tested) |
| Confidence presented as certainty | UI copy: «سطح شواهد (نه قطعیت)» |

## 5. Migration

`20261108054311_AddPersonalImprovementAndCoach`

- **Up:** creates 4 tables; unique indexes on baselines & snapshots; FK `CoachMessages → CoachThreads` cascade  
- **Down:** drops in safe order  
- No FK to Users/Workspaces (consistent with several existing domain tables) — isolation enforced in services  
- Residual risk (medium/low): orphan rows if user deleted without cascade — acceptable for Beta if app soft-deletes / keeps users

## 6. Test / build results

| Suite | Result |
|-------|--------|
| Backend `dotnet test` (full) | **Passed** (after clean; prior failure was stale StaticWebAssets from concurrent frontend build into `wwwroot`) |
| Frontend `npm test` | **12/12 passed** |
| Frontend `npm run build` | **Succeeded** (chunk-size warnings only) |

Focused tests added/updated: opt-in privacy, fabricated evidenceRefs discard, foreign thread / AI disabled, sparkline source, SocialGamification opt-in seeds.

## 7. Critical / high findings

| Severity | Finding | Status |
|----------|---------|--------|
| **HIGH** | Leaderboard treated missing privacy as visible | **Fixed** (opt-in) |
| **HIGH** | Coach accepted provider `evidenceRefs` | **Fixed** (server-only) |
| Medium | Chart could be read as historical mastery | **Mitigated** (explicit source note) |
| Medium | Phase 12 uncommitted; depends on unmerged Phase 11 | Process — PR sequence |
| Low | No FK from baselines/threads to Users | Documented |

## 8. Beta acceptability

**Acceptable for Beta** after the 12.1 fixes above, **provided**:

1. Phase 11 lands first (or stacked PR is intentional).  
2. Phase 12 working tree is committed under a separate release gate.  
3. Product accepts opt-in leaderboard semantics (users who previously appeared without a privacy row become private until they opt in — aligned with approved “Default private”).

## 9. Follow-ups (not Phase 13)

- Commit/push Phase 12 under release-gate approval.  
- Optional: backfill `UserPrivacySettings` for active members if ops want an explicit audit trail (not required for correctness after opt-in).  
- Avoid parallel `npm run build` + `dotnet test` without clean when wwwroot is shared.
