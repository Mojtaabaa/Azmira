# Existing Product Features

Classification key:

- **Fully implemented** — UI + backend path verified in rewrite (`frontend` + `backend`) unless noted
- **Partially implemented** — works but incomplete for target product ambitions
- **UI only / Backend only / Not implemented / Unable to verify**

Live production at the provided URL was **Unable to verify** (HTTP 404). Classifications below are from **repository implementation**.

## Account and identity

| Feature | Status | Notes |
| --- | --- | --- |
| User registration | Fully implemented | Username 3–32 `[A-Za-z0-9_.]`, password 8–128 |
| Login / logout | Fully implemented | Cookie session 30 days |
| Session restore (`/auth/me`) | Fully implemented | |
| User profile (name, email, avatar, bio) | Not implemented | Only `id` + `username` |
| Password reset / email verification | Not implemented | |
| OAuth / SSO | Not implemented | |
| Roles (student/teacher/admin/parent) | Not implemented | Single end-user role only |
| Administration console | Not implemented | |

## Student / school management

| Feature | Status | Notes |
| --- | --- | --- |
| Student management (rosters) | Not implemented | Product is personal account only |
| School management | Not implemented | |
| Classrooms / cohorts | Not implemented | |
| Teacher portal | Not implemented | |
| Parent portal | Not implemented | |
| Organization / tenant switcher | Not implemented | |

## Study tracking

| Feature | Status | Notes |
| --- | --- | --- |
| Study session recording | Fully implemented | `sessions` collection |
| Focus / Pomodoro timer | Fully implemented | work/break/long break lengths in settings |
| Manual session entry | Fully implemented | timer page forms |
| Daily / weekly goals | Fully implemented | settings + dashboard meters |
| Per-subject weekly goals | Fully implemented | settings + analytics chart |
| Subject catalog + custom subjects | Fully implemented | built-ins + custom IDs |
| Topic tracking board | Fully implemented | `topics` + coverage % |
| Planner / calendar events | Fully implemented | `events`, Jalali toggle, week grid |
| Timer → planner sync | Fully implemented | setting `syncSessionsToPlanner` |
| Tasks / to-dos | Fully implemented | filters including overdue |
| Assignments from teachers | Not implemented | tasks are personal only |

## Assessment

| Feature | Status | Notes |
| --- | --- | --- |
| Test recording | Fully implemented | answer sheet + manual forms |
| Score calculation (konkoor formula) | Fully implemented | `utils/score` |
| Mistake logging + review queue | Fully implemented | causes taxonomy |
| Learning assessment bank / quizzes authored by school | Not implemented | |

## Analytics and reports

| Feature | Status | Notes |
| --- | --- | --- |
| Study hours charts | Fully implemented | day/week/month grain |
| Test counts / score trends | Fully implemented | |
| Subject share / goals progress | Fully implemented | |
| Task completion analytics | Fully implemented | |
| Topic weakness table | Fully implemented | |
| Mistake cause chart | Fully implemented | |
| Streak / consistency KPIs | Fully implemented | dashboard + analytics |
| Weekly PNG report | Fully implemented | canvas export |
| Progress comparisons between students/schools | Not implemented | no multi-user visibility |
| Parent/teacher dashboards | Not implemented | |

## Notes / knowledge

| Feature | Status | Notes |
| --- | --- | --- |
| Markdown notes vault | Fully implemented (rewrite) | SQL-backed in rewrite; IndexedDB in legacy |
| Folders, trash, restore | Fully implemented | |
| Wikilinks, tags, graph view | Fully implemented | client-side |
| Note images | Fully implemented | stored as data URLs |
| Notes sync across devices | Fully implemented in rewrite | **Partially** in legacy (device-local) |

## Import / export / backup

| Feature | Status | Notes |
| --- | --- | --- |
| Study JSON backup (legacy style) | Fully implemented | settings page historically |
| Full account export/import (study + notes + images) | Fully implemented (rewrite) | `/account/export|import`, Data page |
| CSV export sessions/tests/tasks | Fully implemented (rewrite) | Data page |
| Browser legacy data adoption | Fully implemented | AuthProvider + adopt helpers |
| Notes vault ZIP import/export | Fully implemented | client-side |

## Notifications

| Feature | Status | Notes |
| --- | --- | --- |
| Browser notification helper for timer | Partially implemented | utility exists; not a full notification center |
| Email / push campaigns | Not implemented | |
| In-app notification inbox | Not implemented | |

## UX shell

| Feature | Status | Notes |
| --- | --- | --- |
| RTL Persian UI | Fully implemented | |
| Light/dark/system theme | Fully implemented | |
| Command palette (Ctrl+K) | Fully implemented | |
| Responsive mobile bottom nav | Fully implemented | |
| Lazy-loaded secondary routes | Fully implemented (rewrite) | analytics/notes/etc. |

## Explicit product gaps requested in Phase 0 checklist

| Asked area | Status |
| --- | --- |
| User profiles (rich) | Not implemented |
| Student management | Not implemented |
| School management | Not implemented |
| Roles and permissions | Not implemented |
| Administration | Not implemented |
| Cross-user progress comparisons | Not implemented |

## Feature maturity verdict

ROTBEH today is a **mature single-learner study OS** for konkoor preparation. It is **not** yet a school SaaS platform. Most “future ROTBEH” capabilities in Task 6 are greenfield relative to the current schema and APIs.
