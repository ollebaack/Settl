# Household statistics — spec

A new **Statistik** top-level nav tab for a single household, showing per-person "fun"
data over time. v1 is deliberately one graph: **who paid how much, per person, per
month** — answering "which household member fronted how much, and when." Aggregation
lives in the API per [ADR-0006](../adr/0006-api-first-contract.md) (UIs render and call,
never compute); charts render with shadcn's chart components (Recharts).

Provenance: decided via `/grill` on 2026-07-17. No companion ADR — nothing here is a
hard-to-reverse architectural choice. It applies the existing API-first contract
(ADR-0006) and states the one new dependency (`recharts`) inline, which the root rule
("no new dependencies without stating why first") allows.

## Problem

Settl exposes plenty of household data — `GET /households/{id}/entries` carries
`Category`, `Date`, `AmountMinor`, `PaidByMemberId` and per-member `Shares`
([EntryDtos.cs](../../apps/api/Settl.Api/Features/EntryDtos.cs)) — but nothing surfaces
it as a trend. Members can see the current net on Home and the itemized log in the
book, but not "who has been carrying the household over time." There is no reporting or
aggregation endpoint today; `entry-categories.md` explicitly parks spend reporting as
out of scope. This spec adds the first read-only, per-person, time-bucketed view.

## Scope (v1)

- **One graph — Who-paid-over-time.** Monthly buckets on the x-axis, one series (color)
  per current household member, value = sum of `AmountMinor` on entries that member
  paid (`PaidByMemberId`) in that month, by `Entry.Date` (the occurred date, not
  `CreatedAt`). Grouped or stacked bars.
- **Single household.** The page reads one household's data. **No** cross-household or
  multi-book aggregation — user↔household is many-to-many, but this view never merges
  books.
- **Trailing 12 months, monthly buckets.** Endpoint accepts `from`/`to`; the page
  defaults to the last 12 whole months, **clipped at the front to the household's first
  entry** so young households don't render a flat empty runway (established households
  with older history stay pinned to the 12-month cap). No user-facing range picker in v1.
- **Household totals framed per person** — i.e. the *payer* dimension, in minor units.
  **Not** the viewer's personal share; this is "who fronted what," not "my spending."
- **Read-only.** No drill-down, no export, no filtering by category/member in v1.
- **Reuses existing money conventions** — integer minor units, single household
  currency (`Household.Currency`), UTC/`DateOnly` dates. No new money math client-side.

## Data model

None. No new tables, columns, or enums. Balances remain derived
([BalanceCalculator.cs](../../apps/api/Settl.Api/Domain/BalanceCalculator.cs)); this
feature only aggregates existing `Entry` rows. No migration.

## API surface

New read endpoint:

- **`GET /households/{id}/stats/contributions?from={date}&to={date}`**
  → `ContributionStatsDto`.
  - `from`/`to` are `DateOnly` (inclusive `from`, exclusive `to` recommended); when
    omitted the API defaults to the trailing 12 whole months ending at the current
    month, with the start clipped forward to the first attributable payment's month
    (no leading empty months for young households). An explicit `from` disables the
    clip. Bucketing is monthly, server-side.
  - Auth/membership check mirrors the other `households/{id}/*` endpoints (caller must
    be a member).
  - Shape (illustrative — finalize in implementation):
    - `Currency: string`
    - `Members: [{ MemberId, Name, AvatarColor, AvatarEmoji }]` — the members that
      appear as series (household members with any contribution in range).
    - `Buckets: [{ Month: "2026-07", PerMember: [{ MemberId, PaidMinor }] }]` — one
      entry per month in range (zero-filled for months with no activity, so the chart
      has a continuous axis).
  - Aggregates only `Entry` rows with a non-null `PaidByMemberId`, summing
    `AmountMinor` by `(member, month-of-Date)`. Includes both `Expense` and
    `RecurringPost` entry types (recurring posts are real spend).
- **api-client regeneration in the same PR** (root rule): add `ContributionStatsDto`
  and the endpoint to `packages/api-client` from the live OpenAPI.
- Covered by API tests: membership enforcement, default-range behavior, month
  zero-filling, payer attribution, minor-unit sums.

## Web surface

- **New route + nav tab** `Statistik`. Rather than adding a 5th tab (the mobile bar is a
  fixed 4-slot + FAB grid), `Statistik` **takes the `Aktivitet` tab slot** in the
  hardcoded nav in
  [app-shell.tsx](../../apps/web/src/components/app-shell.tsx) (`BarChart3Icon`), plus a
  route file under `apps/web/src/routes/`. This keeps 4 tabs and avoids restructuring
  `MobileTabBar`. **Coordinate with ADR-0020's nav rework** (home = overview, `Hushållet`
  book tab) — that work is already editing `app-shell.tsx`; land this without clobbering it.
- **Nudges (Knuffar) relocate off the tab bar.** With `Aktivitet` gone, the `/activity`
  route stays but its mobile entry point becomes a **bell button in the mobile header**
  (`BellIcon`, `aria-label="Knuffar"`, with an unread dot when nudges exist) that
  navigates to `/activity`. Desktop is unchanged — it keeps the right-rail "Knuffar" panel,
  so the header bell is mobile-only. Reuses the existing `useNudges` hook and `/activity`
  screen; no nudge logic changes.
- Fetches via a new TanStack Query hook in
  [queries.ts](../../apps/web/src/lib/queries.ts) against the endpoint above; renders a
  grouped/stacked bar chart.
- **Charts: shadcn chart components (Recharts).** New dependency: `recharts`.
  Justification: shadcn-native (matches the existing shadcn/Base UI setup), themeable
  via the `--chart-1..5` CSS variables, copy-paste components the repo owns rather than
  a wrapped abstraction; confirmed compatible with React 19 + Tailwind v4 and installed
  cleanly under pnpm (which resolves the `react-is` override that trips npm users). Add
  the shadcn `chart` component and map member series to `--chart-*` colors.
- Member colors: prefer each member's `AvatarColor` for series identity so the chart
  reads consistently with avatars elsewhere; fall back to `--chart-*` if needed.
- Empty state: households with no entries in range show a friendly empty chart, not an
  error.

## Out of scope / open questions

Deferred graphs (rejected for v1, recorded as future scope):
- **Category-split-per-person** — which categories each member tends to pay for.
- **Fun leaderboard / superlatives** — top spender, biggest single expense, streaks.
- **Net-balance trend over time** — rejected, not just deferred: balances are never
  snapshotted, so a who-owes-whom line requires replaying entries + settlement
  closures; high cost, low marginal value given current net is already on Home.
- **Client-side aggregation** — rejected: violates ADR-0006.

Open questions:
- Series identity when a member leaves the household mid-range — include their
  historical contributions (by `MemberId`) or drop them? v1 leans toward including any
  member with contributions in range.
- User-selectable time range (3/6/12/all) — deferred; revisit if the fixed 12-month
  window proves limiting.
- Whether to add the deferred "fun" graphs later warrants its own grill once v1 ships
  and there's usage signal.
