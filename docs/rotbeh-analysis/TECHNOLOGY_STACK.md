# Technology Stack Discovery

Legend: **Verified** = found in repository manifests/source. **Assumption** = reasonable inference. **Unable to verify** = not observable from repo or live site.

## 1. Rewrite stack (recommended forward path)

### Backend

| Item | Finding | Evidence |
| --- | --- | --- |
| Language | C# | `backend/**/*.cs` |
| Framework | ASP.NET Core Minimal APIs | `backend/Program.cs`, endpoint mappers under `backend/Api/` |
| Runtime / TFM | **.NET 10.0** | `backend/KonkoorHelp.Api.csproj` → `net10.0` |
| ORM | Entity Framework Core SQL Server **10.0.6** | package reference + `AppDbContext` |
| Database | SQL Server (LocalDB in development) | connection string key `ConnectionStrings:KonkoorHelp` |
| Schema bootstrap | `Database.EnsureCreated()` (no EF migrations folder) | `Program.cs` |
| AuthN | Cookie session (`kh_session`), token hashed with SHA-256, stored in `AuthSessions` | `SessionService.cs` |
| Password hashing | ASP.NET Core Identity `PasswordHasher<object>` | `PasswordService.cs` |
| AuthZ | Implicit owner-only: every data query filters by session `user.Id` | stores + endpoints |
| API style | REST-ish JSON under `/api` | auth/data/notes/account endpoint groups |
| Validation | Custom JSON validators (`StudyValidation`) + size limits | `StudyValidation.cs`, Kestrel body limit 48 MB |
| Rate limiting | In-memory attempt limiter for login/register | `AttemptLimiter.cs` |
| Background jobs | **None found** | no hosted services / Hangfire / Quartz |
| Cache / messaging | **None found** | no Redis/Rabbit/SignalR usage |
| File storage | Note images as **data URLs in SQL** (`NoteImages.DataUrl`) | `Entities.cs`, notes API |
| OpenAPI/Swagger | **Not present** in rewrite project file | OpenApi package was removed from csproj |
| Tests | **No unit/integration test project** for .NET | no `*Tests*.csproj` |

### Frontend (rewrite)

| Item | Finding | Evidence |
| --- | --- | --- |
| Language | TypeScript (loose: widespread `// @ts-nocheck`) | `frontend/src/**/*.tsx` |
| UI library | React **19.2.8** | `frontend/package.json` |
| Bundler | Vite **8.3.0** | `frontend/package.json`, `vite.config.ts` |
| Routing | react-router-dom **7.18.4** | `App.tsx` |
| State | Custom sync store for study data + Zustand for notes UI | `utils/storage.ts`, `features/notes/store/*` |
| Charts | Recharts **3.10.1** | analytics components |
| Editor | CodeMirror + `@uiw/react-codemirror` | notes feature |
| Icons | lucide-react | package.json |
| CSS | Custom CSS (no Tailwind/Bootstrap/MUI) | `index.css`, page CSS files |
| Fonts | Vazirmatn, Estedad | `@fontsource/*` |
| Client DB remnant | Dexie only for **legacy guest-note migration** | `legacyNotesDb.ts` |
| Component library | **None** (hand-built components) | no MUI/Chakra/Ant Design deps |

### Deployment (rewrite)

| Item | Finding | Evidence |
| --- | --- | --- |
| Publish script | `publish.ps1` builds frontend into `backend/wwwroot`, then `dotnet publish` → `publish/` | root script |
| Hosting artifacts | `web.config` present in publish output (IIS-capable) | publish verification during prior work |
| Reverse proxy support | Forwarded headers middleware enabled | `Program.cs` |
| Production config file | `backend/appsettings.Production.json` (connection string placeholder for local SQL) | file exists; **values not reproduced here** |

## 2. Legacy stack (still in repository root)

| Item | Finding | Evidence |
| --- | --- | --- |
| Backend | Node.js + Express **5.3.0** | root `package.json`, `server/` |
| Database | SQLite via better-sqlite3 **13.0.3** | `server/db.js` |
| Frontend | React 19 + Vite (JavaScript JSX) | root `src/`, `vite.config.js` |
| Auth | Cookie session, scrypt password hashes | `server/auth.js` |
| Notes | Dexie IndexedDB in browser | `src/features/notes/lib/db.js` |
| Smoke test | `server/smoke.mjs` (`npm run test:api`) | package scripts |
| Lint | ESLint 10 | `eslint.config.js` |

## 3. Integrations

**Verified present (local/product utilities, not third-party SaaS):**

- Browser notifications helper (`utils/notify`)
- LocalStorage theme preference
- File download (JSON backup, CSV, PNG weekly poster, ZIP notes)

**Verified absent:**

- Payment providers
- Email / SMS providers
- OAuth / social login
- External LMS / school SIS connectors
- Analytics SaaS SDKs
- AI provider SDKs

## 4. Production environment

| Check | Result |
| --- | --- |
| `https://totbeh.viannacloud.ir/` | **Unable to verify app** — HTTP 404 during audit |
| Hosting provider / IIS / Kestrel on that host | Unable to verify from repository |
| Which commit is deployed | Unable to verify |

## 5. Version snapshot (verified from manifests)

- .NET TFM: `net10.0`
- EF Core SqlServer: `10.0.6`
- React: `19.2.8`
- Vite: `8.3.0`
- TypeScript (frontend rewrite): `5.9.3`
- Express (legacy): `5.3.0`
- better-sqlite3 (legacy): `13.0.3`
