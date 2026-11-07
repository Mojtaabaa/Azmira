# User Roles

## Platform

| Role | Meaning |
|------|---------|
| Regular user (`PlatformRole` 0) | Standard account |
| Elevated (`PlatformRole` ≥ 100) | Platform admin tooling (where exposed) |

## Workspace membership

| Role | Personal | School |
|------|----------|--------|
| Student | Owner of personal WS | Learner |
| Teacher | — | Class-scoped teaching |
| SchoolAdministrator | — | School settings / members |
| SchoolOwner | — | Full school control |
| Parent | — | Linked children only |

Workspace header `X-Rotbeh-Workspace` selects tenant. Cross-workspace access returns `403 workspace_forbidden`.

## API keys

Owned by a user, bound to specific workspaces, further limited by scopes. Revoke immediately if leaked.
