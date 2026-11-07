# Frontend Architecture

## Basis

Extend existing rewrite app: **React 19 + TypeScript + Vite + React Router + custom CSS + Zustand (notes) + sync store (`utils/storage`)**.

Do not introduce a second SPA. Add shells/modules inside `frontend/`.

Live production already serves this SPA from IIS (`https://rotbeh.viannacloud.ir`).

## 1. High-level structure (target)

```
frontend/src/
  app/                 # providers, router, i18n, workspace context
  shells/
    personal/          # current learner UX
    school/            # role-aware school UX
    parent/            # guardian UX
    admin/             # platform admin (limited)
  modules/
    identity/
    workspace/
    study/             # timer, topics (wrap existing pages)
    planner/
    assessment/
    analytics/
    gamification/
    school/
    notifications/
  shared/
    ui/                # buttons, cards, meters (extract gradually)
    api/
    auth/
    i18n/
    layout/
  pages/               # keep existing routes during transition
  features/notes/      # keep as-is module
```

Migration approach: **strangler** — existing `pages/*` continue to work; new modules wrap them; move files gradually.

## 2. Routing

Keep BrowserRouter. Add workspace-aware routes:

| Path | Shell |
| --- | --- |
| `/` … existing personal routes | Personal |
| `/w/:workspaceId/...` optional later | Explicit |
| `/school/...` | School shell |
| `/parent/...` | Parent shell |
| `/admin/...` | Platform admin |

Until path-based workspaces ship, use header + client context only (compatible).

Lazy-load school/parent/gamification routes like current analytics/notes.

## 3. Workspace context

```tsx
// conceptual
WorkspaceProvider {
  workspaces, activeWorkspaceId, role, switchWorkspace()
  api.fetch injects X-Rotbeh-Workspace
}
```

On switch:

1. Persist preference via API
2. Clear `storage` memory + notes hydrate
3. Reload workspace-scoped data

## 4. i18n (Persian / English, RTL / LTR)

| Concern | Design |
| --- | --- |
| Library | Lightweight message dictionaries first (`fa.json`, `en.json`); avoid heavy frameworks unless needed |
| Default | `fa-IR` + `dir=rtl` (current) |
| English | `en-US` + `dir=ltr` |
| HTML | `document.documentElement.lang` + `dir` |
| CSS | Prefer logical properties (`margin-inline`, already partially used) |
| Numbers/dates | `fa-IR` / `en-US` `Intl` formatters (existing clock utils extended) |

Brand strings currently hard-coded Persian — wrap progressively.

## 5. UI system

- Continue custom CSS variables in `index.css` (already themed).
- Extract reusable primitives into `shared/ui` without a third-party kit initially.
- Responsive patterns already exist (sidebar → bottom nav); reuse for school dashboards.
- Charts: keep Recharts.
- Icons: lucide-react.

Support mobile-friendly KPI cards (existing dashboard patterns).

## 6. State management

| Domain | Store |
| --- | --- |
| Auth / workspace | React context |
| Study collections | Existing `utils/storage` sync store |
| Notes | Existing Zustand stores |
| Gamification summary | Small query cache / Zustand slice |
| Server lists (classes) | Page-local state → React Query later if complexity grows |

Do not force Redux.

## 7. API client

Extend `utils/api.ts` `request()`:

- Attach `X-Rotbeh-Workspace`
- Preserve `credentials: 'same-origin'`
- Keep `ApiError` + unauthorized logout behavior

## 8. Permission-aware UI

- Nav builders filtered by `role` from `/auth/me`
- Resource actions disabled/hidden, but errors still handled from API
- Parent shell: read-only components; no timer write entry points

## 9. Accessibility & UX

- Keep RTL semantics and keyboard command palette
- Prefer `prefers-reduced-motion` (already partially handled)
- School dense tables need responsive card fallbacks

## 10. Compatibility risks

| Risk | Mitigation |
| --- | --- |
| Large hard-coded Persian UI | Incremental i18n; don’t big-bang |
| `@ts-nocheck` debt | New modules typed strictly; old files gradual |
| Dual `src/` legacy tree | Do not edit legacy for features |
| Workspace switch cache bugs | Central invalidate helper |
| Bundle size (notes/analytics) | Keep lazy routes; school modules lazy |

## 11. Delivery slices (frontend)

1. WorkspaceProvider + switcher (personal only visible)
2. i18n plumbing + English skeleton
3. Gamification widgets on personal dashboard
4. School shell + teacher/student nav
5. Parent shell
