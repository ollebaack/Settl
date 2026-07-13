# Dev bar — spec

A debug-only diagnostics bar for the web app, in the spirit of Linear's dev bar.
Surfaces live console errors and recent API requests while developing, plus a
"copy to markdown" button that snapshots debug context for pasting into a PR,
issue, or Slack message.

Decided via `/grill` on 2026-07-13. Not ADR-worthy (reversible, dev-only, no
product-behavior impact) and not tech debt (net-new tool, not a shortcut).

## Scope

DEV-only, same precedent as the existing dev user-switcher
([user-switcher.tsx:24](../../apps/web/src/components/user-switcher.tsx#L24)):

```ts
if (!import.meta.env.DEV) return null
```

Never rendered in a production build. No feature flag, no env var — the guard
is the gate.

## UI

**Collapsed state:** a thin bar docked to the bottom of the viewport, full
width, always visible in DEV. Shows a status summary (e.g. error count badge,
last request status) at a glance without opening anything.

**Expanded state:** clicking the bar opens it into a panel (use `Drawer` —
already installed, vaul-based) with `Tabs` for **Console** / **Requests** /
**Info**, each in a `ScrollArea`. A **Copy to markdown** button and a **Clear**
button live in the panel header.

**Layout interaction:** on mobile the app already has a fixed bottom tab bar
([app-shell.tsx:312](../../apps/web/src/components/app-shell.tsx#L312)) with
safe-area padding. The dev bar sits *above* the mobile tab bar (stack, not
overlap) and spans full width on desktop where there's no competing bottom
chrome. Z-index above route content, below any open domain sheet.

Build with shadcn primitives already in the project (`drawer`, `tabs`,
`scroll-area`, `badge`, `button`) per the web rules — no hand-rolled overlay.

## Capture

**Console errors** — uncaught only:

```ts
window.addEventListener('error', handler)
window.addEventListener('unhandledrejection', handler)
```

No monkey-patching `console.error`/`console.warn`. Known, accepted gap:
errors that are caught and merely logged via `console.error()` inside app code
won't appear.

**Requests** — instrument the single choke point in
[api.ts](../../apps/web/src/lib/api.ts), the `request()` function all
`apiGet`/`apiPost`/etc. calls go through. Record per request:

- method
- path
- status (or `"error"` if the fetch threw / network failed)
- duration (ms)
- timestamp

No request/response bodies captured (keeps the buffer and the copied markdown
small; bodies aren't needed to spot a failing or slow endpoint).

**Timing** — both buffers start recording at app boot in DEV, not lazily when
the panel first opens, so whatever just happened is already in the log when
you open it.

**Storage** — two module-level ring buffers (same external-store pattern as
[stores.ts](../../apps/web/src/lib/stores.ts), no new dependency), capped at
the **last 20 entries each**. In-memory only — resets on a hard reload, not
persisted to localStorage.

## Copy-to-markdown

Button generates a markdown document and copies it to the clipboard
(`navigator.clipboard.writeText`). Contents:

- Acting dev user (memberId + name) and active household — from
  [dev-user.tsx](../../apps/web/src/lib/dev-user.tsx) and
  [active-household.tsx](../../apps/web/src/lib/active-household.tsx)
- Current route/path
- Timestamp of capture
- Last 20 console errors (message + stack, timestamp)
- Last 20 requests (method, path, status, duration, timestamp)

Suggested shape:

```md
## Dev snapshot — 2026-07-13T14:32:10Z

- User: Olle (member-abc123)
- Household: Ledningen (household-xyz789)
- Route: /ledger

### Console errors (3)
- 14:31:58 — TypeError: Cannot read properties of undefined (reading 'id')
  at EntryRow (entry-row.tsx:42)

### Requests (20)
- 14:32:05 · GET /households/xyz789/summary · 200 · 84ms
- 14:32:01 · POST /entries · 422 · 41ms
```

## File map (for implementation)

- `apps/web/src/lib/dev-bar-store.ts` — ring buffers + listeners (console,
  requests), external-store pattern
- `apps/web/src/lib/api.ts` — add a capture call inside `request()`
- `apps/web/src/components/dev-bar.tsx` — the bar + drawer UI, `if
  (!import.meta.env.DEV) return null` guard
- Mount `<DevBar />` in `app-shell.tsx` (or wherever `user-switcher.tsx` is
  currently mounted)

## Out of scope / open questions

- No React error boundary is introduced by this spec — uncaught render errors
  rely on the browser's global `error` event, same as any other uncaught
  exception.
- Redaction: not addressed. Everything captured is local dev-only traffic
  (pre-auth, tech-debt 0003), so no sensitive-data handling is specced here.
  Revisit if this tool survives past the ADR-0005 auth work.
