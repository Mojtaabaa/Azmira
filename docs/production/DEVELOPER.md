# Developer Documentation

## Layout

- `backend/` — ASP.NET Minimal APIs, EF Core, hosted workers
- `backend.Tests/` — xUnit (engines, school, learning, integrations)
- `docs/` — architecture + production guides

## Conventions

- Route maps in `backend/Api/*Endpoints.cs`
- Domain services under `Services/{Gamification,Learning,Schools,Integrations}`
- Workspace isolation via `WorkspaceContext` + `X-Rotbeh-Workspace`
- Domain events → `DomainOutbox` → `GamificationPipeline` → optional `WebhookService`

## Adding a public v1 route

1. Implement/reuse a service method with explicit `workspaceId`.
2. Map under `PublicApiEndpoints.MapPublicApiV1` with `HasScope`.
3. Document in `docs/production/PUBLIC_API.md`.
4. Add a multi-tenant / scope test when authz is non-trivial.

## Testing

```bash
dotnet test backend.Tests/KonkoorHelp.Tests.csproj
```

InMemory tests must use unique DB names and explicit user Ids when multiple users are created.

## Security checklist for PRs

- Query always filters by `WorkspaceId`
- No raw SQL with string concat
- Mutations: Origin (cookie) or API key
- Secrets hashed (API keys) or never logged
