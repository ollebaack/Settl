---
name: api-logs
description: Read the local API server's log file when debugging a failing request, 500 error, or unexpected server behavior in dev. Use whenever the user reports an API error, pastes a dev-bar snapshot with a failing request, or asks "why did this fail" / "check the logs" for apps/api.
---

# API logs

The API (`apps/api/Settl.Api`) writes its own structured logs to a local file whenever
it runs in Development, via `FileLoggerProvider`
([FileLoggerProvider.cs](../../../apps/api/Settl.Api/Services/FileLoggerProvider.cs)),
registered in [Program.cs](../../../apps/api/Settl.Api/Program.cs). This exists because
the API normally runs under the Aspire AppHost (`pnpm dev`), whose console/dashboard
output isn't otherwise reachable from a tool session.

## Where

`apps/api/Settl.Api/.logs/api.log` — gitignored, **truncated every time the API process
starts** (same "fresh every `pnpm dev` start" precedent as the dev Postgres reset), so
it only ever contains the current session's logs, not stale history from a previous run.

## How to use it

1. Read or Grep `apps/api/Settl.Api/.logs/api.log` directly — plain text, one line per
   log call, format `[HH:mm:ss.fff] [Level] Category: message`, with the exception's
   `ToString()` appended on the following lines when one was logged.
2. Correlate by timestamp against whatever failing request you're chasing — e.g. a
   dev-bar snapshot's `POST /households/.../invites · 500 · 4661ms` line — to find the
   matching server-side log entries around that time.
3. Search for the relevant category/keyword first (`Grep -i "error"`, the endpoint name,
   an exception type) rather than reading the whole file if it's grown large.

## Caveats

- If the file doesn't exist or looks stale (timestamps don't cover the request you're
  investigating), the API process hasn't been restarted since `FileLoggerProvider` was
  wired in, or isn't running — say so rather than guessing from an empty/old file.
- This is dev-only tooling. Don't suggest wiring it into anything that ships.
