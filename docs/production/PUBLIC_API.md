# ROTBEH Public API (v1)

Base path: `/api/v1`  
JSON camelCase. Errors: `{ "error": "…", "code": "…" }`.

## Authentication

| Mode | How |
|------|-----|
| Session cookie | `kh_session` (browser app) |
| API key | Header `X-Api-Key: rk_…` |

Workspace context: `X-Rotbeh-Workspace: {guid}` (required for most resources; API keys are bound to one or more workspaces).

Create/revoke keys (session required):

- `GET /api/integrations/api-clients`
- `POST /api/integrations/api-clients` `{ "name", "scopes": ["*"] }`
- `POST /api/integrations/api-clients/{id}/revoke`

Scopes use `resource:action` (e.g. `sessions:read`). `*` grants all. Session auth ignores scopes.

## Endpoints

| Method | Path | Scope | Notes |
|--------|------|-------|-------|
| GET | `/auth/me` | — | User + workspaces + auth mode |
| GET | `/workspaces` | `workspaces:read` | Memberships |
| GET | `/users` | `users:read` | Active workspace members |
| GET | `/schools/me` | `schools:read` | School profile |
| GET | `/classes` | `classes:read` | Classes |
| GET | `/subjects` | `subjects:read` | School subjects |
| GET | `/study-sessions` | `sessions:read` | Recent sessions |
| POST | `/study-sessions` | `sessions:write` | Create session (emits gamification events) |
| GET | `/study-plans` | `plans:read` | AI planner suggestions |
| GET | `/assessments` | `assessments:read` | Catalog |
| GET | `/assessments/{id}` | `assessments:read` | Detail (no answers) |
| GET | `/analytics/me` | `analytics:read` | Profile + mastery |
| GET | `/analytics/school` | `analytics:read` | School dashboard |
| GET | `/gamification/me` | `gamification:read` | XP / level / streak |
| GET | `/quests` | `quests:read` | Active quests |
| GET | `/achievements` | `achievements:read` | Badges |
| GET | `/leaderboards` | `leaderboards:read` | `?metric=&period=&classId=` |
| GET | `/rewards` | `rewards:read` | Grants for current user |
| GET | `/notifications` | `notifications:read` | Game notifications |

Cookie mutations still enforce Origin checks. API-key clients skip Origin (machine-to-machine). API keys are rate-limited (~120 req/min per key+IP).

Existing app routes under `/api/*` remain the primary UI surface; `/api/v1` is the documented integration contract.
