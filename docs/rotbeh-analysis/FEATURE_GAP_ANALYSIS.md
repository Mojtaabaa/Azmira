# Feature Gap Analysis vs Future ROTBEH Capabilities

For each capability: existing implementation, missing pieces, dependencies, likely DB/API/UI changes, and risks.

## 1. Personal Workspace

| | |
| --- | --- |
| Existing | **Fully present** as the entire product (per-user account) |
| Missing | Explicit Workspace entity; profile metadata |
| Dependencies | None |
| DB | Optional `Workspaces` table with type=`Personal` |
| API/UI | Minimal if default workspace is implicit |
| Risk | Low |

## 2. School Workspace

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | School entity, membership, workspace switcher, school settings |
| Dependencies | Multi-tenant model, RBAC |
| DB | Schools, Memberships, WorkspaceId on data |
| API/UI | School onboarding, switcher, admin screens |
| Risk | High (cross-cutting) |

## 3. Multi-Tenant Architecture

| | |
| --- | --- |
| Existing | Single-tenant-per-user isolation only |
| Missing | Tenant/workspace keys on all owned rows; policies |
| Dependencies | AuthZ overhaul |
| DB | TenantId/WorkspaceId everywhere hot data lives |
| API/UI | Context header or path segment |
| Risk | High — must be designed before school features |

## 4. Study Management

| | |
| --- | --- |
| Existing | Sessions, subjects, topics, goals — strong |
| Missing | Curriculum templates, school-assigned study plans |
| Dependencies | School workspace |
| DB | Prefer typed session columns later |
| API/UI | Extend existing pages |
| Risk | Low–Medium |

## 5. Study Planner

| | |
| --- | --- |
| Existing | Weekly planner + events — strong |
| Missing | Shared class calendars, teacher-published schedules |
| Dependencies | School roles |
| DB | Calendar ownership/sharing |
| API/UI | Extend planner |
| Risk | Medium |

## 6. Focus Timer

| | |
| --- | --- |
| Existing | Fully implemented Pomodoro/timer store |
| Missing | School policy caps, supervised mode |
| Dependencies | Optional school rules engine |
| DB/API/UI | Small extensions |
| Risk | Low |

## 7. Learning Assessment

| | |
| --- | --- |
| Existing | Personal test/mistake capture — strong for self-assessment |
| Missing | Question banks, graded assignments, teacher review workflows |
| Dependencies | Teacher portal |
| DB | Assessment items, attempts, grades |
| API/UI | Major new modules |
| Risk | High |

## 8. Learning Analytics

| | |
| --- | --- |
| Existing | Personal analytics + weekly PNG — strong |
| Missing | Cohort analytics, school dashboards, comparisons |
| Dependencies | Multi-tenant data + typed metrics |
| DB | Aggregates / warehouse-ish tables |
| API/UI | New dashboards |
| Risk | Medium–High (JSON store limits) |

## 9. Gamification Engine

| | |
| --- | --- |
| Existing | Not implemented (streak exists as metric only) |
| Missing | Event bus of study actions → rules → rewards |
| Dependencies | Reliable study event stream |
| DB | Game profiles, rule definitions |
| API/UI | Engine + feedback toasts |
| Risk | Medium |

## 10. XP and Level System

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | XP ledger, levels, anti-cheat caps |
| Dependencies | Gamification engine |
| DB | XpLedger, Levels |
| API/UI | Profile widgets |
| Risk | Medium (abuse via fake sessions) |

## 11. Badge and Achievement System

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Badge catalog, unlock rules, display |
| Dependencies | XP/events |
| DB | Badges, UserBadges |
| API/UI | Gallery |
| Risk | Low–Medium |

## 12. Quest and Challenge System

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Quest definitions, progress, rewards |
| Dependencies | Gamification + study events |
| DB | Quests, QuestProgress |
| API/UI | Quest board |
| Risk | Medium |

## 13. Study Streak

| | |
| --- | --- |
| Existing | **Fully implemented** as analytics/dashboard KPI |
| Missing | Streak shields, freezes, school competitions |
| Dependencies | Optional gamification |
| DB/API/UI | Extend existing streak util |
| Risk | Low |

## 14. Leaderboard

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Rankings by workspace/class with privacy controls |
| Dependencies | Multi-tenant + XP or study metrics |
| DB | Leaderboard snapshots |
| API/UI | Rank pages |
| Risk | Medium (privacy, fairness) |

## 15. Team Competition

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Teams, matches, scoring |
| Dependencies | School workspace + leaderboard |
| DB | Teams, TeamMembers, Competitions |
| API/UI | Team UX |
| Risk | High |

## 16. Avatar and Virtual Store

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Avatar inventory, currency, catalog |
| Dependencies | XP/currency economy |
| DB | Items, Inventory, Purchases |
| API/UI | Store screens |
| Risk | Medium |

## 17. Learning Journey

| | |
| --- | --- |
| Existing | Partial via topics coverage + notes graph |
| Missing | Guided paths, milestones, adaptive sequencing |
| Dependencies | Curriculum content model |
| DB | Paths, PathProgress |
| API/UI | Journey map |
| Risk | Medium–High |

## 18. AI Learning Coach

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Provider integration, prompts, safety, cost controls |
| Dependencies | User consent, data export boundaries |
| DB | Coach conversations, usage meters |
| API/UI | Coach chat panel |
| Risk | High (cost, privacy, hallucination) |

## 19. School Management

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | School CRUD, branding, policy config |
| Dependencies | Multi-tenant |
| DB | Schools, SchoolSettings |
| API/UI | Admin console |
| Risk | High |

## 20. Teacher Portal

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Class management, assignments, student oversight |
| Dependencies | School + RBAC + assessment |
| DB/API/UI | Large new surface |
| Risk | High |

## 21. Parent Portal

| | |
| --- | --- |
| Existing | Not implemented |
| Missing | Guardian links, limited read dashboards, consent |
| Dependencies | RBAC + privacy model |
| DB | GuardianStudent links |
| API/UI | Parent app/shell |
| Risk | High (privacy) |

## 22. Public API and Webhooks

| | |
| --- | --- |
| Existing | Private cookie API only |
| Missing | API keys/OAuth clients, scopes, webhook deliveries, signed payloads |
| Dependencies | Stable resource model + audit logs |
| DB | ApiClients, WebhookEndpoints, DeliveryLogs |
| API/UI | Developer settings |
| Risk | Medium–High |

---

## Reuse vs extend vs new

| Category | Items |
| --- | --- |
| **Reuse as-is** | Focus timer, personal planner, tests/mistakes capture, notes vault, personal analytics charts, backup/export |
| **Extend** | Auth (add roles/workspaces), study records (add WorkspaceId + typed fields), streak → gamification hooks, analytics → cohort views |
| **New implementation** | Tenancy, school/teacher/parent portals, XP/badges/quests/store, competitions, AI coach, public API/webhooks |

## Dependency backbone

```
Multi-Tenant / Workspace
   ├─ School Management
   │    ├─ Teacher Portal
   │    ├─ Parent Portal
   │    └─ Team Competition
   ├─ Learning Assessment (school-graded)
   └─ Cohort Analytics / Leaderboards
Study Events (existing sessions/tests)
   └─ Gamification Engine
        ├─ XP/Levels
        ├─ Badges
        ├─ Quests
        └─ Store / Avatar
Public API (after stable resources)
AI Coach (after privacy + data contracts)
```
