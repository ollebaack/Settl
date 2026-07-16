# ADR-0019: Adaptive home and the multi-household overview

- **Status:** accepted
- **Date:** 2026-07-16

## Context

Wishlist: "En översiktssida så man kan se alla hushåll/grupper … Borde ligga som
'hem'?" Today every screen — Hem, Loggbok, På repeat, Aktivitet, and the `+` FAB — is
scoped to a single **active household**; switching books is a bottom sheet
(`household-switcher-sheet`). A cross-household overview is the one surface that would
ignore the active-household context the rest of the app depends on, so *where* it lives
is an information-architecture decision, not a layout choice. The load-bearing
constraint: most users belong to exactly one household, and they must not pay a tax
(an extra tap, an empty one-card screen) for a feature that only benefits
multi-household users. Grilled 2026-07-16.

## Decision

- **"Hem" (`/`) is adaptive on the count of the user's *active* households.** Zero →
  the existing first-run create flow. Exactly one → that household's dashboard,
  unchanged (net hero, per-person rows, upcoming, recent) — the overview does not exist
  for single-household users. Two or more → `/` renders the **multi-household
  overview**.
- **The overview lists every active book** as a card with its server-derived net
  position (`HouseholdListItemDto`: net, netLabel, memberNames) and an **Arkiverade**
  section for archived households (ADR-0016). Tapping a book sets the active household
  and enters it; Loggbok/Repeat/Aktivitet/FAB keep following the active household as
  today. The household-switcher sheet stays as the in-context switch from other screens.
- **No global cross-currency total.** Net positions are only summed into a headline
  when every active book shares one currency; otherwise the hero shows a descriptive
  roll-up (how many books you're owed in / owe in), never an illegal sum across
  currencies.
- **The overview carries the "add friend/contact" entry point** from the wishlist. Its
  data model is decided separately in the contacts-by-phone workstream (grilled
  2026-07-16, landing on a parallel branch — add-by-number = a blind SMS invite,
  contacts reusable across households). This ADR commits only to the *affordance* on the
  overview, not the contacts screen or its endpoints.

## Consequences

Adaptive routing means `/` has two shapes and the app needs an explicit "in a book"
view distinct from the overview for the ≥2 case (the current home layout, reused);
whether that is one route that swaps or a dedicated per-household route is left to the
implementing PR — the design fixes the screens, not the routing. The switcher sheet now
overlaps the overview for multi-household users (two ways to change book); we keep both
because the sheet is the only in-context switch from Loggbok/Repeat/Aktivitet. The
adaptive threshold is keyed on *active* (non-archived) households, so archiving the
second-to-last book silently collapses Hem back to the single-household dashboard —
acceptable and self-explanatory. The overview reads only fields already on
`HouseholdListItemDto`; no new list contract is required, though the add-friend
affordance depends on the contacts/invite endpoints from the parallel contacts ADR
landing (numbering to be reconciled at merge — this branch currently ends at ADR-0016).

Explicitly rejected: **overview-always-as-home** (taxes the single-household majority
with an extra tap to a one-card screen every launch); a **separate 5th nav
destination** ("Översikt"/"Hushåll") — creates two competing homes, buries the overview,
and ignores the wishlist's "borde ligga som hem?" instinct; and a **summed global net
figure** (meaningless across currencies).
