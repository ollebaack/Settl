# @settl/api-client

TypeScript types generated from the Settl API's OpenAPI spec. This is the contract
both the web app and the future mobile app consume — the API is the source of truth,
UIs are disposable (see ADR-0006).

## Regenerate

1. Start the API: `pnpm dev:api` (from repo root)
2. `pnpm --filter @settl/api-client generate`

Commit the regenerated `src/schema.d.ts` together with the API change that caused it.
