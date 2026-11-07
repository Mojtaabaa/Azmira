# Performance Notes

## Already in place

- Composite indexes on study records, outbox, webhook deliveries (`Status, NextAttemptAt`), API key hash uniqueness
- `AsNoTracking` on read-heavy paths
- Outbox + webhook workers batch with backoff (avoids request-path fan-out)
- Leaderboard page size capped; analytics windows bounded

## Guidance

- Prefer workspace-scoped queries; avoid loading full collections when `take` exists (`/api/v1/study-sessions`)
- Large reports (AI weekly) are cached per `periodKey` in `AiWeeklyReports`
- Do not add Redis/message buses until single-node workers + SQL indexes are insufficient
- Watch N+1 in new Include chains; materialize `Contains` lists for teachers’ class filters (already done in school analytics)

## Background jobs

| Worker | Purpose |
|--------|---------|
| `OutboxProcessor` | Gamification pipeline |
| `WebhookDispatcher` | Signed HTTP deliveries + retries |
