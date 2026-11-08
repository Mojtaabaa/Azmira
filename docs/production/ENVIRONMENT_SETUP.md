# Environment Setup

## Local development

1. Install .NET 10 SDK and SQL LocalDB (or SQL Server).
2. From `backend/`: set connection in `appsettings.Development.json` or use default LocalDB string.
3. `dotnet run` — migrations apply automatically.
4. Frontend (Vite): `localhost:5173` is allowed by Origin checks.

Optional AI:

```json
"Ai": { "DefaultProvider": "heuristic" }
```

## Staging / production

- Put live secrets in **`sandbox/appsettings.json`** on the server (see `sandbox/README.md`). This file is listed in `PRESERVE.manifest.json` and is not replaced by `install-rotbeh.ps1`.
- Each release ships `sandbox/appsettings.defaults.json`. On startup (and after install), **missing keys are merged in**; existing values are never overwritten.
- Optional override: env var `ROTBEH_SANDBOX` = absolute path to the sandbox directory.
- `appsettings.Production.json` in the package stays empty on purpose; sandbox / env supply real values.
- Enable HTTPS at the proxy; `ForwardedHeaders` is already wired.
- Configure `Security:AllowedOrigins` for any non-same-origin browser clients.
- Set `Integrations:WebhookProtectionKey` (sandbox) or `Integrations__WebhookProtectionKey` (env) to a Base64 32-byte key.
- Health: `/health/live` (liveness), `/health/ready` (DB), `/health` (combined).

### Generate a webhook protection key

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

Or in C#: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`.

## File uploads / payload limits

Kestrel `MaxRequestBodySize` = 48 MB (notes images). Validate content types in notes pipeline; do not expose raw file paths.
