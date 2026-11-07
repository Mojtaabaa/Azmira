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

- Prefer environment variables over checked-in JSON for secrets.
- `appsettings.Production.json` ships with empty connection string on purpose.
- Enable HTTPS at the proxy; set `ForwardedHeaders` is already wired.
- Configure `Security:AllowedOrigins` for any non-same-origin browser clients.
- Set `Integrations__WebhookProtectionKey` to a Base64 32-byte key (required in Production for new webhook secrets).
- Health: `/health/live` (liveness), `/health/ready` (DB), `/health` (combined).

### Generate a webhook protection key

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

Or in C#: `Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))`.

## File uploads / payload limits

Kestrel `MaxRequestBodySize` = 48 MB (notes images). Validate content types in notes pipeline; do not expose raw file paths.
