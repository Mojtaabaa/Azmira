# Phase 10.2 — Daily Missions Reliability, Transaction Integrity & Security Fixes

**Branch:** `feat/phase-10-daily-missions`  
**HEAD (unchanged commit):** `6e72dd953f81e7d7d4c3d4fe3bf4335c8386fe1d`  
**Working tree:** dirty (Phase 8–10 + RBAC uncommitted; no push/PR)  
**Migration for Phase 10.2:** **Not required** (no new schema; no new DB constraints added in this phase)  
**Status:** STOP — awaiting approval (do not start Phase 11)

---

## Verification

| Suite | Result |
|-------|--------|
| `Phase102ReliabilityTests` + `DailyMissionTests` | **24 passed** |
| Gamification / School / PlatformRbac / HttpPublic filter | **33 passed** |
| Full backend `KonkoorHelp.Tests` | **86 passed** |
| Frontend `npm test` | **11 passed** |
| Frontend `npm run build` | **OK** |

Baseline HEAD recorded before edits: `6e72dd9`. History not rewritten. No push/merge/deploy.

---

## Finding status (F1–F15)

### F1 — Atomic claim and reward publication — **Fixed**

- **Files:** `QuestEngine.cs`, `OutboxPublisher.cs`
- **Details:** `ClaimAsync` opens a DB transaction, conditionally transitions `Completed → Claimed`, `EnqueueAsync`s `QuestCompleted` with stable key `quest:{questId}:{userId}:completed`, then `SaveChanges` + commit. Publisher only persists with the claim. `EnsureRewardOutboxAsync` recovers Claimed rows missing outbox (legacy/partial failure). Duplicate claim is idempotent.
- **Relational:** `ExecuteUpdate` conditional transition. **InMemory:** gate + reload (provider lacks ExecuteUpdate).
- **Tests:** `F1_claim_enqueues_outbox_atomically_and_is_idempotent`, `F1_recovery_publishes_missing_outbox_for_claimed`, `F5_concurrent_claims_single_winner`, DailyMission claim idempotency.
- **Gap:** Injected mid-transaction crash is covered by transactional rollback semantics; no fault-injection harness.

### F2 — Concurrent daily mission assignment — **Fixed** (no migration)

- **Files:** `DailyMissionService.cs`, `DailyMissionIds.cs`
- **Details:** Deterministic quest/progress PKs per `(workspace, user, localDate, template)` so concurrent inserts collide on PK. Assignment runs in a transaction with `UserGameStats` touch. Caps: max 3 missions, XP/coin budgets, ≤1 time-based. `DbUpdateException` on conflict treated as success.
- **Migration:** Stopped — not needed; unique index on QuestId alone is insufficient, hence deterministic multi-column identity via PK.
- **Tests:** `F2_concurrent_assignment_caps_at_three` (true `Task.WhenAll`).
- **Residual:** EF InMemory ignores isolation; SQL Server relies on PK + transaction. Multi-instance races still collapse via deterministic IDs.

### F3 — Evidence windows — **Fixed**

- **Files:** `DailyMissionService.cs` (`SyncEvidenceAsync`)
- **Details:** Issued missions measure evidence with persisted `StartsAt`/`EndsAt`. Sync covers all Active/Completed daily missions (not only today’s display set). MissionClock used at assignment and for **legacy** rows missing windows.
- **Legacy policy:** `LegacyMissingWindowPolicy` — meta localDate bounds in *current* workspace TZ (best-effort).
- **Tests:** `F3_evidence_uses_persisted_window_not_current_tz` (before/at/near-end/after EndsAt + TZ change).

### F4 — Timezone change / duplicate daily sets — **Fixed**

- **Files:** `DailyMissionService.cs` (`ResolveStableLocalDateAsync`)
- **Details:** If any issued dm UTC window still covers `now`, freeze to that meta `localDate` — no second overlapping active set. New clock day only when no covering window remains.
- **Tests:** `F4_timezone_change_does_not_open_second_active_day`, DST bounds test.

### F5 — Concurrent claim transition — **Fixed**

- **Files:** `QuestEngine.ClaimAsync`
- **Details:** Conditional Completed→Claimed (ExecuteUpdate on relational; gated reload on InMemory). Losers see Claimed and run recovery (no double reward). Same txn as F1 outbox enqueue.
- **Tests:** `F5_concurrent_claims_single_winner` (10 parallel claims).

### F6 — Durable teacher confirmation audit — **Fixed**

- **Files:** `QuestEngine.ConfirmManualAsync`
- **Details:** Same transaction as completion: `AuditLog` action `quest.manual_confirm` with teacher/student/workspace/quest + `noteSha256` (raw note not stored). Not relying on `GameNotification` as audit. Duplicate confirm skips second audit.
- **Tests:** `F6_teacher_confirm_writes_audit_and_rejects_wrong_workspace`, DailyMission teacher confirm.

### F7 — Workspace-local review due date — **Fixed**

- **Files:** `DailyMissionService.CountReviewsDueAsync`
- **Details:** Uses `MissionClock.ToLocalDate(UtcNow, workspaceTz)` — not `DateTime.UtcNow.Date`.
- **Tests:** `F7_review_due_uses_mission_clock_local_date` (Tehran vs UTC around midnight).

### F8 — XP transaction and daily caps — **Fixed** (with residual multi-node note)

- **Files:** `XpEngine.ApplyAsync`
- **Details:** Serializable transaction + learner grant gate; always write XP ledger for EventId idempotency (even delta 0); local-day XP cap via MissionClock; quest coin cap at grant (`MaxDailyMissionCoins`); unique EventId indexes unchanged (pre-existing).
- **Migration:** Not required (existing unique EventId indexes).
- **Tests:** `F8_duplicate_event_delivery_does_not_double_xp`, `F8_concurrent_distinct_events_respect_daily_xp_cap`.
- **Residual:** Process-local `SemaphoreSlim` supplements SQL isolation; multi-process correctness depends on Serializable + unique EventId on real SQL Server.

### F9 — Time-based mission mix — **Fixed**

- **Files:** `DailyMissionService` (`TimeBasedConditions`, assignment loop)
- **Details:** Hard max one of `study_minutes` / `session_count` per learner per day.
- **Tests:** `F9_catalog_marks_time_based_conditions`, `F9_at_most_one_time_based_mission_assigned`.

### F10 — Completed missions after expiry — **Fixed**

- **Policy:** **24h claim grace** after `EndsAt` (`QuestEngine.ClaimGraceAfterEndsAt`). After grace → `Expired`, no reward. Enforced in `ClaimAsync` and `ExpireStaleAsync`.
- **Tests:** `F10_completed_past_grace_expires_without_claim`.

### F11 — Teacher confirmation scope — **Fixed**

- **Files:** `QuestEngine.ConfirmManualAsync`
- **Details:** Quest must match `WorkspaceId == current workspace` (rejects `WorkspaceId == null` / other workspace).
- **Tests:** `F11_rejects_workspace_null_quest_confirm`, F6 cross-workspace.

### F12–F14 — **Not applicable**

- Not listed as Phase 10.2 remediation targets in the user mandate (audit numbering jumped to F15 for coverage). No separate work items assigned.

### F15 — Automated coverage — **Partially fixed**

- **Added:** `Phase102ReliabilityTests` — concurrent claims, concurrent assignment, duplicate XP delivery, XP cap concurrency, cross-workspace confirm, manual confirm + audit, DST, TZ change, evidence windows, grace expiry, deterministic IDs.
- **Closest reliable boundary for teacher auth:** service/engine tests (workspace scope + existing SchoolEdition teacher ownership patterns). Dedicated HTTP 401/403 for `POST /teacher/missions/{id}/confirm` not newly added in this pass.
- **Partial transaction failure:** covered by transactional design + recovery path; no chaos/fault-injection suite.

---

## Transaction & idempotency guarantees

| Path | Guarantee |
|------|-----------|
| Claim | Same txn: status→Claimed + outbox enqueue; idempotency key unique; recovery if Claimed without outbox |
| Assignment | Deterministic PKs + txn; ≤3 / day; ≤1 time-based; XP/coin assignment budgets |
| Evidence | Persisted UTC window; legacy meta+current TZ fallback documented |
| XP/Coins | EventId uniqueness; local-day caps; quest coin cap at grant |
| Teacher confirm | Txn: progress + AuditLog; workspace-scoped quest |

## New DB constraints / migrations

- **None for Phase 10.2.** (Pre-existing school registration migration remains from earlier RBAC work on the dirty tree; not introduced by 10.2 remediations.)

## Remaining risks

1. EF InMemory cannot prove SQL isolation; production must use SQL Server.
2. Multi-instance claim/XP races rely on relational conditional update + unique indexes (+ process gates only help one host).
3. HTTP-level teacher confirm authorization still depends on endpoint middleware; add an explicit WebApplicationFactory case in a follow-up if desired.
4. Frontend build emits `backend/wwwroot/assets/*` — do not commit generated artifacts unless intentionally publishing.

## Preserve checklist

- Phase 7.2 security controls, Phase 8 docs, Phase 9 navigation/home/AvatarPortrait: untouched by redesign.
- Quest/XP compatibility preserved; personal/school isolation preserved via workspace scoping.

---

**STOP after Phase 10.2.** No push, PR, merge, deploy, or Phase 11 until explicit approval.
