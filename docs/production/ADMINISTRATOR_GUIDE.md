# Administrator Guide

## Platform admins (multi-admin)

- Panel: `/platform` (requires `PlatformRole` ≥ 100).
- Promote/demote platform admins (`POST /api/platform/users/{id}/role`). The last platform admin cannot be removed.
- Approve or reject school registration requests.
- Create a school directly for an existing user (`ownerUserId`).
- Dashboard KPIs: users, admins, schools, pending requests.

## School admins / owners

- Switch to the school workspace, then manage members, years, grades, subjects, classes.
- Assign Teacher / Parent / Student (and related) roles — cannot assign or demote the last `SchoolOwner` casually.
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
- After deploy, ensure at least one platform admin exists (`ROTBEH_BOOTSTRAP_PLATFORM_ADMIN_USERNAME` if needed).
