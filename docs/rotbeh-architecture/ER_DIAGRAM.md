# Entity Relationship Diagram

Logical ER for the target design. Existing tables marked with `*`.

## Overview (Mermaid)

```mermaid
erDiagram
  Users ||--o{ AuthSessions : has
  Users ||--o| Settings : has
  Users ||--o{ Workspaces : owns
  Users ||--o{ WorkspaceMembers : joins
  Schools ||--|| Workspaces : "school workspace"
  Workspaces ||--|| WorkspaceSettings : has
  Workspaces ||--o{ WorkspaceSubscriptions : has
  Workspaces ||--o{ WorkspaceMembers : includes
  Workspaces ||--o{ StudyRecords : scopes
  Workspaces ||--o{ Notes : scopes
  Workspaces ||--o{ DomainOutbox : emits
  Workspaces ||--o{ XpLedger : tracks
  Users ||--o{ XpLedger : earns
  DomainOutbox ||--o| XpLedger : "idempotent award"
  GamificationProfiles ||--o{ WorkspaceSettings : configures
  Workspaces ||--o{ Classes : contains
  Classes ||--o{ ClassMembers : has
  Classes ||--o{ Assignments : assigns
  Users ||--o{ GuardianStudentLinks : parent
  Users ||--o{ GuardianStudentLinks : student

  Users {
    int Id PK
    string Username
    string PasswordHash
    tinyint PlatformRole
    guid LastWorkspaceId
  }
  Workspaces {
    guid Id PK
    tinyint Type
    string Name
    guid SchoolId FK
    int OwnerUserId FK
    tinyint Status
  }
  WorkspaceMembers {
    guid Id PK
    guid WorkspaceId FK
    int UserId FK
    tinyint Role
    tinyint Status
  }
  Schools {
    guid Id PK
    string Name
    int OwnerUserId FK
  }
  StudyRecords {
    int Seq PK
    int UserId FK
    guid WorkspaceId FK
    string Collection
    string ItemId
    string Data
  }
  DomainOutbox {
    guid Id PK
    guid WorkspaceId FK
    string EventType
    string IdempotencyKey
    tinyint Status
  }
  XpLedger {
    guid Id PK
    guid WorkspaceId FK
    int UserId FK
    guid EventId UK
    int Delta
  }
```

## Relationship rules

1. `Users 1—* AuthSessions*` (existing)
2. `Users 1—0..1 Settings*` (legacy personal settings during transition)
3. `Users 1—1 Personal Workspace` (enforced by unique filtered index)
4. `Schools 1—1 School Workspace`
5. `Workspaces 1—* WorkspaceMembers`
6. `Workspaces 1—* StudyRecords*` (after backfill)
7. `Workspaces 1—* Notes*/NoteFolders*/…` (after backfill)
8. `DomainOutbox 1—0..1 XpLedger` via unique `EventId`
9. School instructional graph hangs under School Workspace only

## Index highlights

| Table | Index |
| --- | --- |
| Workspaces | UX_PersonalOwner (`OwnerUserId`) WHERE Type=Personal AND DeletedAt IS NULL |
| Workspaces | UX_School (`SchoolId`) WHERE Type=School |
| WorkspaceMembers | UX_Member (`WorkspaceId`,`UserId`) WHERE DeletedAt IS NULL |
| StudyRecords | IX (`WorkspaceId`,`Collection`,`Seq`) |
| DomainOutbox | UX (`WorkspaceId`,`EventType`,`IdempotencyKey`) |
| DomainOutbox | IX Pending (`Status`,`NextAttemptAt`) |
| XpLedger | UX (`EventId`) |
| UserGameStats | PK (`WorkspaceId`,`UserId`) |

## Soft-delete visibility

Application queries default to `DeletedAt IS NULL`. Platform admin tools may include deleted rows explicitly.
