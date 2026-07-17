# ADR-0020: Home is the household overview; the book merges dashboard and log

- **Status:** accepted
- **Date:** 2026-07-16

## Context

Home (`/`) currently branches on active-household count: 1 household renders the
per-household dashboard directly, 2+ renders the multi-household Overview. That
adaptive behavior shipped alongside the ADR-0019 commit but was never itself recorded
as a decision. It makes "home" mean two different things and hides the Overview from
the single-household majority. Separately, the dashboard and Loggbok (`/ledger`) are
two tabs that read as summary-then-detail of the same book, and "Gör upp" (settle) is
only reachable by tapping a per-person balance row — easy to miss. Grilled 2026-07-16.

## Decision

- **Home is always the Overview.** `/` renders the multi-household portfolio for
  everyone; the adaptive "1 household → dashboard" branch is removed. Picking a book
  sets it active and navigates to the book tab.
- **A single book tab merges dashboard + Loggbok** into one scroll (net hero →
  per-person balances → upcoming → filter pills → chronological log). It replaces the
  separate Loggbok tab; the nav stays four tabs (Hem, Hushållet, På repeat,
  Aktivitet). One component powers both this tab (showing the active household) and the
  `/hushall/$id` drill-in from the Overview — one merged view, two entry points.
- **"Gör upp" stays per-person, surfaced prominently** on each balance row of the
  merged book page. We do not add a global/aggregate "settle everything" action.

## Consequences

A single-household user's Hem is a one-card Overview — accepted, because a dedicated
book tab is one tap away, so there is no net extra tap versus today's Loggbok tab. The
`/ledger` route is repurposed (or renamed) into the merged book view and `/ledger` as a
standalone log goes away; deep links to it must redirect. The book tab is labelled
**Hushållet**; the exact page layout is settled in the ui-design pass. Keeping
settlement per-person preserves pure-ledger settlement (ADR-0007) with no new aggregate
API surface; the cost is that a user who owes several people still settles them one at a
time. Revisit if telemetry shows most users are single-household (the always-Overview
home would then be pure overhead) or if a "settle all" flow becomes a common ask.

Explicitly rejected: keeping the adaptive home (home means two things, hides the
Overview); Overview-with-single-household-auto-redirect (back button lands on a page the
user never chose); log as an in-page tab or no merge at all (didn't fit the one-scroll
goal); and a global aggregate settle button (new settlement semantics and confirm flow,
not worth it now).
