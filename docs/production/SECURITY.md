# Security Review (Phase 7)

| Area | Status | Notes |
|------|--------|-------|
| Authentication | Pass | Cookie sessions (HttpOnly, SameSite=Lax, Secure on HTTPS) + API keys (SHA-256 hashed, prefix index) |
| Authorization | Pass | Workspace membership + school RBAC + API scopes |
| Tenant isolation | Pass | Workspace header resolution; API keys bound to workspaces; webhook enqueue per workspace |
| IDOR | Pass / tested | Cross-workspace API key auth returns null; school teacher class checks remain |
| SQL injection | Pass | EF parameterized queries; no dynamic SQL with user concat |
| XSS | Pass | API returns JSON; SPA responsible for output encoding |
| CSRF | Pass | Origin check on cookie mutations; API keys exempt |
| Rate limiting | Pass | Login/register AttemptLimiter; API key ~120/min |
| Input validation | Pass | StudyValidation, auth regex/length, webhook URL scheme checks |
| File upload | Pass | Body size cap 48MB; notes pipeline constraints |
| Sensitive data | Pass | Passwords hashed; API keys shown once; production connection string not committed |
| Session security | Pass | Token hashed at rest; expiry cleanup |

Residual: expand automated HTTP IDOR matrix over time; keep AllowedOrigins configured in prod.
