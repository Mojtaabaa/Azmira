# Repository Structure

## Top-level layout (verified)

```
konkoor-help/
  backend/          ASP.NET Core rewrite API + EF Core
  frontend/         React + TypeScript SPA rewrite
  server/           Legacy Express API + SQLite
  src/              Legacy React JS SPA
  publish/          Built server deploy package (generated)
  publish.ps1       Build+publish script for rewrite
  docs/rotbeh-analysis/   Phase 0 audit docs (this folder)
  package.json      Legacy Node app manifests
  README.md         Vite template README (not product docs)
```

## Entry points

### Rewrite

| Role | Path |
| --- | --- |
| API host | `backend/Program.cs` |
| Frontend bootstrap | `frontend/src/main.tsx` → `App.tsx` |
| Dev proxy | `frontend/vite.config.ts` proxies `/api` → `127.0.0.1:5080` |
| Production static files | served from `wwwroot` by ASP.NET after publish |

### Legacy

| Role | Path |
| --- | --- |
| API host | `server/index.js` → `server/app.js` → `server/api.js` |
| Frontend | `src/main.jsx` → `src/App.jsx` |
| Dev embedding | root `vite.config.js` mounts Express middleware in Vite |

## Backend modules (rewrite)

| Directory / file | Responsibility |
| --- | --- |
| `backend/Api/AuthEndpoints.cs` | register/login/logout/me |
| `backend/Api/DataEndpoints.cs` | study collections CRUD + settings + bulk replace |
| `backend/Api/NotesEndpoints.cs` | notes vault, folders, trash, images, note settings |
| `backend/Api/AccountEndpoints.cs` | full account export/import |
| `backend/Api/StudyValidation.cs` | settings/collection JSON normalization |
| `backend/Services/*` | password, session, study store, notes store, account service, limiter |
| `backend/Data/Entities.cs` | EF entities |
| `backend/Data/AppDbContext.cs` | schema mapping / indexes |
| `backend/appsettings*.json` | connection strings and logging (secrets not documented) |

## Frontend modules (rewrite)

| Area | Path | Responsibility |
| --- | --- | --- |
| Shell / nav | `components/Layout.tsx` | sidebar groups, command palette, mobile nav |
| Auth UI | `components/auth/*`, `pages/LoginPage.tsx` | gate + forms |
| Dashboard | `pages/DashboardPage.tsx`, `components/dashboard/*` | home KPIs, habits, next step |
| Timer | `pages/TimerPage.tsx`, `utils/timerStore.ts` | Pomodoro + session logging |
| Tests / mistakes | `pages/TestsPage.tsx`, `pages/MistakesPage.tsx` | exam capture + review |
| Analytics | `pages/AnalyticsPage.tsx`, `components/analytics/*` | charts + insights |
| Tasks / planner / topics | matching pages under `pages/` | planning surface |
| Notes | `features/notes/**` | Obsidian-like vault |
| Data transfer | `pages/DataPage.tsx` | JSON/CSV/PNG export + import |
| Settings | `pages/SettingsPage.tsx` | goals, theme, subjects, clear data |
| Shared client store | `utils/storage.ts`, `utils/api.ts` | optimistic sync over `/api` |

## API surface (rewrite, verified)

All under `/api`:

- Auth: `POST /auth/register|login|logout`, `GET /auth/me`
- Study: `GET|PUT /data`, `PUT /settings`, `PUT|POST /data/{collection}`, `PATCH|DELETE /data/{collection}/{id}`
- Notes: `/notes/vault`, `/notes/notes`, `/notes/folders`, `/notes/settings`, `/notes/trash`, `/notes/images`
- Account: `GET /account/export`, `POST /account/import`

Collections (`COLLECTION_KEYS`): `sessions`, `tests`, `mistakes`, `topics`, `tasks`, `events`.

## Database models

See `DATABASE_ANALYSIS.md`. There is **no** EF Migrations project/folder.

## Authentication components

- Server: `SessionService`, `PasswordService`, cookie `kh_session`
- Client: `AuthProvider`, `AuthGate`, `useAuth`

## Tests

| Kind | Location | Notes |
| --- | --- | --- |
| Legacy API smoke | `server/smoke.mjs` | scripted HTTP checks |
| Frontend unit/e2e | **Not found** | — |
| .NET tests | **Not found** | — |

## Deployment scripts

- `publish.ps1`: `npm run build` in `frontend/` then `dotnet publish` to `publish/`
- IIS-oriented `web.config` appears in publish output

## Configuration files (names only)

- `backend/appsettings.json`
- `backend/appsettings.Production.json`
- `backend/Properties/launchSettings.json`
- `frontend/vite.config.ts`, `frontend/tsconfig.json`
- root `.gitignore`

**Note:** Do not commit real production secrets into these files. This audit does not print connection string values.
