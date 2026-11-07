# User Roles

## Platform

| Role | Meaning |
|------|---------|
| Regular user (`PlatformRole` 0) | Standard account; may **request** a school, not create one |
| Platform admin (`PlatformRole` ≥ 100) | Multi-admin: manage platform roles, approve/reject school requests, create schools directly |

Bootstrap when no admin exists: set `ROTBEH_BOOTSTRAP_PLATFORM_ADMIN_USERNAME` (required in Production). In Development, the first user may be promoted automatically.

## Workspace membership

| Role | Personal | School |
|------|----------|--------|
| Student | Owner of personal WS | Learner |
| Teacher | — | Class-scoped teaching |
| AcademicManager | — | Limited school structure |
| SchoolAdministrator | — | School settings / members |
| SchoolOwner | — | Full school control |
| Parent | — | Linked children only |

Workspace header `X-Rotbeh-Workspace` selects tenant. Cross-workspace access returns `403 workspace_forbidden`.

## School creation flow

1. User submits `POST /api/schools/requests`.
2. Platform admin reviews via `/api/platform/school-requests` (approve/reject).
3. On approve, a school workspace is created with the requester as `SchoolOwner`.
4. Platform admin may also `POST /api/platform/schools` (or `POST /api/schools` as admin) to create directly for an `ownerUserId`.

## UI visibility

Nav links for teacher / parent / school analytics appear only when capabilities allow. Regular personal users see school **request** UI, not teacher/parent portals.

`/auth/me` returns `capabilities` for the client.

## API keys

Owned by a user, bound to specific workspaces, further limited by scopes. Revoke immediately if leaked.
