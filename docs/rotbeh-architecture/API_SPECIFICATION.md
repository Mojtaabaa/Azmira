# API Specification (Design)

## Conventions (match existing rewrite)

- Base path: `/api`
- JSON camelCase
- Cookie session `kh_session` (existing)
- Errors: `{ "error": "پیام فارسی یا کلید پایدار" }` (+ optional `code`)
- Mutations: honor Origin checks as today
- Workspace context header: `X-Rotbeh-Workspace: {guid}`  
  If omitted → Personal workspace of current user (backward compatible)

## Auth (existing + small extensions)

### POST `/api/auth/register`

Request:

```json
{ "username": "sara", "password": "password1" }
```

Response `201`:

```json
{ "user": { "id": 2, "username": "sara" }, "personalWorkspaceId": "…" }
```

Side effect: create Personal workspace + membership + settings + subscription.

### POST `/api/auth/login` / `/api/auth/logout` / GET `/api/auth/me`

Keep current shapes; extend `/me`:

```json
{
  "user": { "id": 2, "username": "sara", "displayName": null, "platformRole": 0 },
  "workspaces": [
    { "id": "…", "type": "personal", "name": "فضای شخصی", "role": "student" }
  ],
  "activeWorkspaceId": "…"
}
```

## Workspace

### GET `/api/workspaces`

List memberships for current user.

### POST `/api/workspaces/personal/ensure`

Idempotent ensure Personal workspace (migration helper).

### POST `/api/workspaces/{id}/switch`

Persist `LastWorkspaceId`.

Response:

```json
{ "ok": true, "activeWorkspaceId": "…" }
```

### GET `/api/workspaces/{id}/settings`  
### PUT `/api/workspaces/{id}/settings`

```json
{ "settings": { "dailyGoalMinutes": 240, "localeDefault": "fa-IR" } }
```

### POST `/api/workspaces/{id}/invites`

```json
{ "emailOrUsername": "ali", "role": "teacher" }
```

## Study (extend existing)

Keep:

- `GET/PUT /api/data`
- `PUT /api/settings` (writes active workspace settings; legacy alias)
- `POST/PUT/PATCH/DELETE /api/data/{collection}`

All become workspace-scoped automatically via context.

Example create session:

```http
POST /api/data/sessions
X-Rotbeh-Workspace: 11111111-1111-1111-1111-111111111111
Content-Type: application/json

{ "id": "s1", "subject": "math", "durationMinutes": 25, "startTime": "2026-11-07T08:00:00" }
```

Response `201`: `{ "ok": true }`  
Server also enqueues `StudySessionCompleted` outbox event.

## Planner

Existing events/tasks collections remain. Future school endpoints:

- `GET /api/planner/classes/{classId}/calendar`
- `POST /api/planner/school-events`

## Assessment

Personal tests/mistakes stay on `/api/data/tests` and `/api/data/mistakes`.

School:

### POST `/api/assessments/assignments`

```json
{ "classId": "…", "title": "آزمون میان‌ترم فیزیک", "dueAt": "2026-11-20T12:00:00Z" }
```

### POST `/api/assessments/assignments/{id}/submit`

```json
{ "answers": [], "score": 72.5 }
```

Emits `AssessmentSubmitted` / `AssessmentPassed` as applicable.

## Analytics

### GET `/api/analytics/summary?grain=week`

Personal or cohort depending on role.

```json
{
  "totalMinutes": 320,
  "streak": 4,
  "averageScore": 68.2,
  "scope": "self"
}
```

### GET `/api/analytics/cohort?classId=…` (Teacher+)

## Gamification

### GET `/api/gamification/me`

```json
{ "xpTotal": 1200, "level": 5, "streakCurrent": 4, "nextLevelXp": 1500 }
```

### GET `/api/gamification/ledger?take=50`

### GET `/api/leaderboards/weekly`

```json
{ "period": "2026-W45", "entries": [{ "userId": 2, "displayName": "سارا", "xp": 400, "rank": 1 }] }
```

## Quests / Achievements / Rewards

- `GET /api/quests`
- `POST /api/quests/{id}/claim`
- `GET /api/achievements`
- `GET /api/rewards/catalog`
- `POST /api/rewards/purchase` `{ "itemId": "…" }`
- `GET /api/rewards/inventory`

## School / Teacher / Parent

- `POST /api/schools` `{ "name": "دبیرستان نمونه" }`
- `GET /api/schools/{id}`
- `GET /api/teacher/classes`
- `POST /api/teacher/classes`
- `GET /api/parent/children`
- `GET /api/parent/children/{studentUserId}/summary`

## Notifications

- `GET /api/notifications`
- `POST /api/notifications/{id}/read`

```json
{ "items": [{ "id": "…", "title": "نشان جدید", "read": false, "createdAt": "…" }] }
```

## Account transfer (existing)

- `GET /api/account/export`
- `POST /api/account/import`

Evolve to include `workspaceId` query/body; default Personal for compatibility.

## Integration API (future)

Versioned under `/api/v1/...` with `Authorization: Bearer {apiKey}`  
Same resource models; no cookie required.

## Error codes (additive)

| code | meaning |
| --- | --- |
| `unauthorized` | no session |
| `workspace_forbidden` | not a member |
| `workspace_required` | missing/invalid workspace |
| `forbidden` | role insufficient |
| `conflict` | duplicate id / idempotency |
| `validation_error` | bad payload |

## Compatibility risks

- Clients ignoring new `/me` fields remain fine.
- Clients writing legacy settings without workspace header continue on Personal.
- School-only endpoints return 403/404 for personal-only users.
