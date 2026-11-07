# Event-Driven Gamification Architecture

## Goal

Study and school actions emit durable domain events. Gamification consumers award XP/badges **exactly once** per logical event, even with retries, crashes, or duplicate client posts.

## 1. Event structure

Envelope (outbox payload + metadata columns):

```json
{
  "eventId": "9f2e…",
  "eventType": "StudySessionCompleted",
  "eventVersion": 1,
  "occurredAt": "2026-11-07T08:25:00Z",
  "workspaceId": "11111111-1111-1111-1111-111111111111",
  "actorUserId": 2,
  "idempotencyKey": "studySession:s1:completed",
  "correlationId": "optional-request-id",
  "data": {
    "sessionId": "s1",
    "subjectId": "math",
    "durationMinutes": 25,
    "source": "timer"
  }
}
```

### Required event types (initial)

| eventType | Typical idempotencyKey |
| --- | --- |
| `StudySessionCompleted` | `studySession:{itemId}:completed` |
| `StudyGoalCompleted` | `studyGoal:{userId}:{date}:{goalKind}` |
| `AssignmentCompleted` | `assignment:{assignmentId}:{userId}:completed` |
| `AssessmentSubmitted` | `assessment:{attemptId}:submitted` |
| `AssessmentPassed` | `assessment:{attemptId}:passed` |
| `MasteryImproved` | `mastery:{userId}:{topicKey}:{level}` |
| `WeeklyGoalCompleted` | `weeklyGoal:{userId}:{weekKey}` |
| `QuestCompleted` | `quest:{questId}:{userId}:completed` |

## 2. Processing model (transactional outbox)

```
API command transaction:
  1. Persist business write (e.g., StudyRecords insert)
  2. Insert DomainOutbox row (Pending) with unique idempotency key
  3. Commit

Processor loop:
  1. Claim Pending rows (UPDATE status / lease)
  2. For each consumer (XpEngine, BadgeEngine, NotificationFanout):
       - If ProcessedEvents has (consumer, eventId) → skip
       - Else apply effect in its own transaction + insert ProcessedEvents
  3. Mark outbox Processed or schedule retry
```

Same SQL Server database initially (no external broker required). Optional later: publish to message bus after outbox commit.

## 3. Idempotency & duplicate prevention

Three layers:

1. **Business unique key** on outbox: `(WorkspaceId, EventType, IdempotencyKey)`  
   Re-posting the same completed session cannot insert a second event.
2. **EventId uniqueness** for ledger: `XpLedger.EventId` unique  
   A processor cannot insert two XP rows for one event.
3. **Consumer inbox**: `(ConsumerName, EventId)` unique  
   Multi-consumer safety and replay protection.

Compensations (revoking XP) create **new** events (`XpReversed`) rather than deleting ledger rows.

## 4. Transaction boundaries

| Step | Boundary |
| --- | --- |
| User API write + outbox insert | Single DB transaction |
| XP award + stats update + inbox mark | Single DB transaction per consumer |
| Notifications | Separate consumer; failure must not roll back XP if XP already committed — rely on inbox retries |

Do **not** award XP inside the request thread unless also writing outbox+ledger atomically with the same idempotency guarantees. Prefer async processor for isolation.

## 5. Retry handling

- Exponential backoff via `NextAttemptAt`
- `Attempts` cap (e.g., 10) → `Failed` + alert
- Poison payloads go to Failed with `LastError`; admin requeue after fix
- Processor must be crash-safe: claim with lease timeout

## 6. Event versioning

- `eventVersion` integer in envelope
- Consumers switch on version; unknown version → retry/fail, never partial interpret
- Additive fields allowed in `data`; renames require new version
- Keep publishers backward compatible for at least one release

## 7. Auditability

- Outbox retains payload until retention purge
- XpLedger is append-only
- AuditLog for manual XP adjustments by admins
- CorrelationId ties HTTP request → outbox → ledger

## 8. Mapping from existing product actions

| Existing action | Emit |
| --- | --- |
| Timer finishes focus / manual session saved | `StudySessionCompleted` |
| Daily/weekly goal threshold crossed | `StudyGoalCompleted` / `WeeklyGoalCompleted` |
| Future assignment submit | `AssignmentCompleted` |
| Test saved with passing score | `AssessmentSubmitted` + maybe `AssessmentPassed` |
| Topic mastery status increases | `MasteryImproved` |
| Quest progress hits complete | `QuestCompleted` |

Streak computation today remains a read model; processor may update `UserGameStats.StreakCurrent` from session events.

## 9. Rule engine sketch

`GamificationProfiles.RulesJson` example:

```json
{
  "version": 1,
  "awards": [
    { "on": "StudySessionCompleted", "xp": 10, "perMinutes": 25, "maxPerDay": 200 },
    { "on": "AssessmentPassed", "xp": 50 }
  ],
  "disabledEvents": []
}
```

Evaluator must use integer math and record rule id in ledger reason for debugging.

## 10. Compatibility risks

- Emitting events for historical imports could flood XP → imports must set `emitEvents:false` or use distinct keys + zero-xp mode
- Client retries on POST session already protected by collection item id uniqueness; align idempotencyKey with item id
- Multi-instance processors need row leasing to avoid double claim (DB unique inbox still saves correctness)
