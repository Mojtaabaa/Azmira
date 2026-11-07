# Permission Matrix

## Roles

### Platform-scoped

| Role | Scope |
| --- | --- |
| Platform Administrator | All tenants (audited) |

### Workspace-scoped

| Role | Code |
| --- | --- |
| School Owner | 60 |
| School Administrator | 50 |
| Academic Manager | 40 |
| Teacher | 30 |
| Student | 10 |
| Parent | 20 |

Personal workspace typically only has **Student** (owner). Owner actions on Personal map to full control of that workspace.

## Legend

- **C** create · **R** read · **U** update · **D** delete · **X** execute/claim · **—** deny  
- **Own** = self-owned rows only · **Class** = assigned class roster · **WS** = entire workspace

## Matrix

| Resource / Action | Platform Admin | School Owner | School Admin | Academic Mgr | Teacher | Student | Parent |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Platform users list | R | — | — | — | — | — | — |
| Create school | C (direct or approve request) | — | — | — | — | request only | — |
| School profile | CRUD | CRUD | RU | R | R | R | R |
| Workspace settings (school) | RU | RU | RU | R | R | R | R |
| Personal workspace settings | — | — | — | — | — | RU own WS | — |
| Invite members | C | C | C | C (limited) | C students? optional | — | — |
| Manage roles | U | U | U | — | — | — | — |
| Classes | CRUD | CRUD | CRUD | CRUD | CRU own | R enrolled | R child |
| Assignments | CRUD | CRUD | CRUD | CRUD | CRUD own class | R/X submit | R child |
| Grades | RU | RU | RU | RU | RU class | R own | R child |
| Study sessions | R all (audit) | R WS | R WS | R WS | R class | CRUD own | R child summary |
| Notes personal | — | — | — | — | — | CRUD own | — |
| Analytics self | — | — | — | — | — | R | — |
| Analytics cohort | R | R | R | R | R class | — | R child |
| Gamification self | — | config | config | — | — | R/X | R child |
| Leaderboard WS | R | RU privacy | RU privacy | R | R | R if enabled | R if enabled |
| Quests/achievements | CRUD catalog | config | config | — | — | R/X | R |
| Rewards store | CRUD | config | config | — | — | R/X | — |
| Competitions | CRUD | CRUD | CRUD | R | R | R/X | R |
| Notifications | R | R | R | R | R | R own | R own |
| Subscription billing | RU | RU | R | — | — | R personal | — |
| API keys / webhooks | CRUD | CRUD | CRUD | — | — | — | — |
| AI coach | config | config | config | — | — | X own | — |
| Audit logs | R | R WS | R WS | — | — | — | — |

## Resource-level checks (mandatory)

Beyond role tables, every handler must:

1. Resolve active workspace membership.
2. Load target resource.
3. Assert `resource.WorkspaceId == context.WorkspaceId` (or platform admin).
4. Apply narrower scope (own / class / child-link).

### Examples

- **Student PATCH session:** member Student + `session.UserId == currentUserId` + same workspace.
- **Teacher GET cohort analytics:** Teacher + `classId` assigned to teacher + same workspace.
- **Parent GET child summary:** Parent + active `GuardianStudentLink` + consent granted + same school workspace.
- **School Admin change role:** cannot demote last School Owner without transfer.

## Permission evaluation order

1. Authenticated?
2. Platform Admin elevation? (still audit)
3. Active membership Active?
4. Role allows action class?
5. Resource scope allows instance?

Fail closed.

## Frontend gating

UI hides unauthorized nav, but **server denies** regardless of UI. Never rely on client role flags alone.
