# Administrator Guide

## School admins / owners

- Register school via `/api/schools`, manage members, years, grades, subjects, classes.
- Configure gamification profile under `/api/admin/gamification/*` when permitted.
- Teacher portal: assignments, missions, feedback, class activity.
- Analytics: `/api/schools/analytics` and `/api/v1/analytics/school`.

## Integrations

1. Create API client under Integrations (`/api/integrations/api-clients`) with least-privilege scopes.
2. Store the raw `rk_…` key offline; only hash is retained.
3. Register webhook HTTPS endpoints; verify `X-Rotbeh-Signature`.
4. Monitor `/api/integrations/webhooks/deliveries` for failures / dead letters.

## Operations

- Watch `/health/ready` and application logs for unhandled API errors.
- Outbox + webhook hosted services process asynchronously; backlog indicates worker/DB issues.
- Rotate API keys by creating a new client then revoking the old one.
