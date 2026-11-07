# Security Audit (Static / Non-Intrusive)

**Method:** code review only. No exploitation attempts against production. Live host returned 404 and was not probed further.

## Strengths (verified)

1. **Password hashing** uses ASP.NET Identity `PasswordHasher` (rewrite) / scrypt (legacy).
2. **Session tokens** are random, stored hashed (SHA-256), HttpOnly, SameSite=Lax; Secure flag when HTTPS.
3. **Per-user data isolation** on every study/notes query via `userId` from session.
4. **Origin check** on non-GET API mutations rejects mismatched `Origin` hosts (with explicit localhost:5173 exceptions for Vite).
5. **Login/register rate limiting** (in-memory) reduces brute-force speed.
6. **JSON size limits** and collection caps reduce oversized payload risk.
7. **Dummy password verify** path reduces obvious username-enumeration timing differences (rewrite PasswordService).
8. Cache-Control `no-store` and `X-Content-Type-Options: nosniff` on API responses.

## Risks and gaps

| Risk | Severity | Notes |
| --- | --- | --- |
| No role-based authorization model | Medium (future) | Fine for personal app; insufficient for school portals |
| In-memory rate limiter | Medium | Resets on process recycle; not shared across instances |
| No CSRF token | Medium | SameSite=Lax mitigates many cases; cross-site POSTs still need review under multi-origin deployments |
| Origin allowlist hardcodes Vite ports | Low–Medium | Production must rely on same-host Origin; reverse-proxy host mismatches can break or weaken checks |
| Forwarded headers trust all proxies | Medium | `KnownIPNetworks/Proxies` cleared — OK only behind a trusted edge |
| EnsureCreated without migrations | Medium ops | Schema drift / accidental wipe risk if mis-operated |
| Study payload is opaque JSON | Medium | Server cannot enforce deep field schemas for all collections equally; malicious oversized nested content possible within size cap |
| Note images as data URLs in SQL | Medium | DB bloat, backup exposure of binary content |
| Account import can replace all data | Medium | Correct for owner, dangerous if session stolen |
| No audit logging of admin-like actions | Low–Medium | No immutable security event trail |
| Dual codebase secrets hygiene | Medium | Ensure production connection strings never committed; this audit did not print them |
| Limited automated security tests | Medium | Only legacy smoke script |
| No security headers suite beyond nosniff | Low | Missing CSP/HSTS configuration in app code (may be provided by reverse proxy — unable to verify) |
| Username enumeration via register 409 | Low | Expected UX tradeoff |
| XSS surface in notes markdown/HTML pipeline | Medium | Client renders markdown/math; requires ongoing sanitization review (not fully proven safe/unsafe here) |

## Cross-user isolation

**Verified:** endpoints load/update/delete only rows matching authenticated `user.Id`.  
**School-level isolation:** N/A — no school entities.

## SQL injection

EF Core parameterized queries used for entity operations. Dynamic SQL string concatenation **not found** in rewrite stores. Legacy uses prepared statements via better-sqlite3.

## Sensitive data handling

- Passwords stored hashed.
- Session token only in cookie (hashed at rest).
- No PII fields (email/phone) currently collected.
- Backups contain full study content and possibly note images — treat export files as sensitive.

## Authorization summary

Current model = **authenticated resource owner**. There is no permission matrix, no impersonation, no delegated access.

## Immediate hardening priorities (recommendations only)

1. Put rate limiting in a durable store before multi-instance hosting.
2. Add CSP and HSTS at reverse proxy or middleware.
3. Introduce EF migrations before any production schema expansion.
4. Move large images to object storage.
5. Add security smoke tests for auth isolation (already partially covered by legacy smoke).
6. Before school features: design explicit authorization policies and tenant boundaries.
