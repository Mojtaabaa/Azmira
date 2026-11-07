# Live Site Verification Addendum

**Date:** 2026-11-07  
**Correction:** The production URL is **`https://rotbeh.viannacloud.ir`** (not `totbeh.viannacloud.ir`).

This addendum updates Phase 0 findings that previously marked the live deployment as unverifiable due to the typo’d hostname.

## Method

Read-only checks only:

- Open the home page in a browser
- Fetch response headers / HTML
- Call `GET /api/auth/me` without credentials

No login attempt with passwords, no mutation requests, no scanning.

## Verified findings

| Check | Result |
| --- | --- |
| Home page HTTP status | **200 OK** |
| Page title | `رتبه` |
| Visible UI | Login/register card: «دفتر مطالعه کنکور», tabs ورود / ثبت‌نام, username + password |
| Server | **Microsoft-IIS/10.0** with **ASP.NET** (`X-Powered-By: ASP.NET`) |
| Front-end assets | Vite hashed bundles, e.g. `/assets/index-BTLKrh54.js` |
| Unauthenticated API | `GET /api/auth/me` → **401** JSON `{"error":"وارد نشده‌ای."}` |

## Interpretation against repository

**Verified:** Production matches the **rewrite stack** published from this repo (`backend` ASP.NET Core + `frontend` React build served as static files), not a bare Node-only host.

Evidence alignment:

- IIS + ASP.NET headers match the `publish/` / `web.config` deployment path
- Auth error Persian string matches rewrite `AuthEndpoints`
- Asset filename `index-BTLKrh54.js` matches the production build previously produced into `backend/wwwroot` / `publish/wwwroot`

**Still unable to verify without authenticated access:**

- Exact database engine/host behind production
- Whether notes are SQL-backed in that environment (expected for rewrite)
- Full feature parity of every page beyond the auth gate
- TLS/security header policy beyond what the login HTML response showed

## Impact on earlier documents

Treat these statements as superseded:

- Any claim that `https://totbeh.viannacloud.ir` is the product URL
- Any claim that live deployment could not be reached / was only 404

Canonical live URL for ROTBEH: **https://rotbeh.viannacloud.ir**

## Phase 0 status

Audit remains implementation-free. Live verification strengthens confidence that the rewrite package is what is deployed.
