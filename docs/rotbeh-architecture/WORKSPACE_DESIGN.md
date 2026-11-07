# Workspace Design

## 1. Goals

- Every learner gets a **Personal Workspace** automatically (no school required).
- Schools operate inside a **School Workspace**.
- One user may belong to many workspaces with different roles.
- Active workspace is an explicit runtime context for API and UI.

## 2. Core entities

### Workspace

| Field | Type | Notes |
| --- | --- | --- |
| Id | `uniqueidentifier` / GUID | PK |
| Type | `tinyint` / enum | `1=Personal`, `2=School` |
| Name | `nvarchar(120)` | Display name |
| Slug | `nvarchar(80)` null | Optional public/school slug |
| SchoolId | FK null | Required when Type=School |
| OwnerUserId | FK Users | Personal: the student; School: founding owner |
| Status | `tinyint` | Active, Suspended, Archived |
| CreatedAt / UpdatedAt | `datetime2` | |
| DeletedAt | `datetime2` null | Soft delete |

**Rules**

- Personal: exactly one active Personal workspace per user (unique filtered index).
- School: exactly one Workspace per School (unique `SchoolId`).
- Soft-deleted workspaces are inaccessible except to Platform Admin recovery tools.

### WorkspaceMember

| Field | Type | Notes |
| --- | --- | --- |
| Id | GUID | PK |
| WorkspaceId | FK | |
| UserId | FK | |
| Role | `tinyint` / enum | See WorkspaceRole |
| Status | `tinyint` | Invited, Active, Suspended, Left |
| InvitedByUserId | FK null | |
| JoinedAt | `datetime2` null | |
| CreatedAt / UpdatedAt | `datetime2` | |
| DeletedAt | `datetime2` null | Soft delete membership |

**Unique:** `(WorkspaceId, UserId)` where `DeletedAt IS NULL`.

### WorkspaceRole (enum values in membership)

Scoped **inside a workspace** (not platform-global):

| Code | Name | Typical use |
| --- | --- | --- |
| 10 | Student | Learner in personal or school workspace |
| 20 | Parent | Guardian linked to student(s) in school workspace |
| 30 | Teacher | Class-level teaching |
| 40 | AcademicManager | Curriculum/assessment oversight |
| 50 | SchoolAdministrator | School operations |
| 60 | SchoolOwner | Highest school authority |

Platform Administrator is **not** a workspace role; it is a global Identity flag (`PlatformRole`).

A user may hold one primary membership role per workspace. If multiple role hats are needed later, introduce `WorkspaceMemberRoles` join table without breaking the single-role MVP.

### WorkspaceSettings

| Field | Type | Notes |
| --- | --- | --- |
| WorkspaceId | PK/FK | 1:1 |
| Data | `nvarchar(max)` JSON | Mirrors today’s settings flexibility |
| LocaleDefault | `nvarchar(10)` | `fa-IR`, `en-US` |
| TimeZone | `nvarchar(64)` | e.g. `Asia/Tehran` |
| FeatureFlags | JSON | Module toggles |
| GamificationProfileId | FK null | Points to tenant-specific ruleset |
| UpdatedAt | `datetime2` | |

Personal workspaces initialize Settings from current `Settings.Data` defaults (pomodoro, goals, subjects).

### WorkspaceSubscription

| Field | Type | Notes |
| --- | --- | --- |
| Id | GUID | PK |
| WorkspaceId | FK | Unique active subscription preferred |
| PlanCode | `nvarchar(40)` | Free, SchoolStarter, SchoolPro, … |
| Status | `tinyint` | Trial, Active, PastDue, Canceled |
| SeatLimit | `int` null | School seats |
| StartsAt / EndsAt | `datetime2` null | |
| ExternalProvider | `nvarchar(40)` null | Future billing |
| ExternalReference | `nvarchar(120)` null | |
| CreatedAt / UpdatedAt | `datetime2` | |

Personal Free plan is implicit/default; still create a row for uniform entitlement checks.

## 3. Lifecycle

### Personal user registration (compatible with today)

1. Create `Users` row (existing).
2. Create Personal `Workspace` owned by user.
3. Create `WorkspaceMember` Role=Student, Status=Active.
4. Create `WorkspaceSettings` from defaults (copy existing user settings JSON).
5. Create Free `WorkspaceSubscription`.
6. Backfill `WorkspaceId` on existing study/notes rows for that user.

### School creation

1. Platform/school owner creates `School`.
2. Create School `Workspace` linked to School.
3. Owner membership Role=SchoolOwner.
4. Subscription/trial row.
5. Invite teachers/students/parents via tokens.

### Joining a school

- Invite or join-code → pending membership → accept → Active.
- Student keeps Personal workspace unchanged.
- School study artifacts live in School workspace (or are shared copies); personal history remains personal unless user opts to share.

## 4. Active workspace resolution

Priority:

1. Request header `X-Rotbeh-Workspace: {guid}` if member Active
2. Else session preference `LastWorkspaceId`
3. Else user’s Personal workspace

Invalid/forbidden workspace → `403` with stable error code `workspace_forbidden`.

## 5. Data ownership model

| Data class | Owner key |
| --- | --- |
| Study sessions/tests/tasks/events/topics/mistakes | `WorkspaceId` + creating `UserId` |
| Notes vault | `WorkspaceId` + `UserId` (personal notes stay personal; school shared notes optional later) |
| XP / badges / quests progress | `WorkspaceId` + `UserId` |
| Class assignments | `WorkspaceId` + school resource ids |
| School config | `SchoolId` / School Workspace |

**Principle:** Personal users never inherit school tenancy. School membership is additive.

## 6. UI implications

- Workspace switcher in shell (hidden if only one membership).
- Personal shell ≈ current app.
- School shell shows role-appropriate nav (teacher/admin/parent/student).
- Switching workspace clears client caches and reloads `/api/data` scoped to workspace.

## 7. Compatibility with existing APIs

Until clients send `X-Rotbeh-Workspace`, server defaults to Personal workspace of the session user — preserving current single-user behavior for `https://rotbeh.viannacloud.ir`.
