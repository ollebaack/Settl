# API rules

- Minimal API endpoints with `.WithName(...)` for stable OpenAPI operation ids.
  Errors return ProblemDetails, not ad-hoc shapes.
- EF Core stays provider-portable — no Postgres-specific SQL/types (ADR-0010).
- Store UTC; money as integer minor units.
- Secrets via user-secrets or env vars, never `appsettings*.json`.
- Money math (splits, balances, recurrence) gets unit tests incl. rounding and
  uneven-split edge cases before it ships.
- New endpoints get a `WebApplicationFactory` integration test (pattern:
  `HealthEndpointTests.cs`).
