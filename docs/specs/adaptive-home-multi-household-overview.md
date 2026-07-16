# Adaptive home & multi-household overview — spec

Make "Hem" (`/`) adapt to how many active households the user belongs to, and add a
cross-household **overview** for people in two or more books. Most users belong to
exactly one household and must not pay a tax (an extra tap, an empty one-card screen)
for a feature that only benefits multi-household users. Visual reference:
[multi-household-overview-addendum.md](../design/multi-household-overview-addendum.md)
(and `docs/design/Settl Multi-Household Overview.dc.html`). Rests on
[ADR-0016](../adr/0016-household-ownership-and-archival.md) (archival) and
[ADR-0019](../adr/0019-contacts-by-phone-and-sms-invites.md) (the add-a-friend
affordance).

Provenance: decided via `/grill` on 2026-07-16. No companion ADR — the choice is an
information-architecture/product call about where a screen lives, reversible in an
afternoon, so it lands as a spec rather than an ADR (per `docs/README.md`). Originally
drafted as "ADR-0019: Adaptive home and the multi-household overview".

## Problem

Every screen today — Hem, Loggbok, På repeat, Aktivitet, and the `+` FAB — is scoped to
a single **active household**; switching books is the bottom sheet
[household-switcher-sheet.tsx](../../apps/web/src/components/sheets/household-switcher-sheet.tsx).
The wishlist asks for "en översiktssida så man kan se alla hushåll/grupper … borde ligga
som 'hem'?". A cross-household overview is the one surface that must ignore the
active-household context the rest of the app depends on, so *where* it lives is an IA
decision, not a layout choice. Today [index.tsx](../../apps/web/src/routes/index.tsx)
renders the single active household's dashboard unconditionally.

## Scope (v1)

- **`/` is adaptive on the count of the user's *active* (non-archived) households.**
  - **Zero** → the existing first-run create flow.
  - **Exactly one** → that household's dashboard, unchanged (net hero, per-person rows,
    upcoming, recent). The overview does not exist for single-household users.
  - **Two or more** → the multi-household **overview**.
- **The overview lists every active book** as a card with its server-derived net
  position (`HouseholdListItemDto`) plus an **Arkiverade** section for archived
  households (ADR-0016, `Återställ` shown to the owner only). Tapping a book sets the
  active household and enters it; Loggbok/Repeat/Aktivitet/FAB keep following the active
  household. The household-switcher sheet stays as the in-context switch from other
  screens.
- **No global cross-currency total.** Net positions are summed into a headline only when
  every active book shares one currency; otherwise the hero shows a descriptive roll-up
  (how many books you're owed in / owe in), never an illegal sum across currencies. The
  client detects >1 distinct `currency` and switches to the descriptive hero.
- **The overview carries the "add friend/contact" affordance** (Overview frame 4). This
  spec commits only to the *affordance and copy* — the contacts model, endpoints, and SMS
  delivery are ADR-0019's territory (see out of scope).
- **No** new bottom-nav slot and **no** overview-as-home for single-household users.

## Data model

No new persistence of its own. Consumes ADR-0016's archival fields (`archivedAt`,
`isOwner`, archived scope). The adaptive threshold counts active books only — archiving
the second-to-last book silently collapses Hem back to the single-household dashboard.

## API surface

Reads only fields already on `HouseholdListItemDto` (`id, name, currency, memberNames,
netMinor, netLabel`); no new list contract is required. Optional, not required: a small
server aggregate (total open count, per-currency subtotals via
`GET /households?include=summary`) to keep hero math out of the client (ADR-0006).
Archived households come from ADR-0016 (`GET /households?scope=archived` +
`POST /households/{id}/restore`). The add-friend affordance depends on the ADR-0019
contacts/invite endpoints. Regenerate `packages/api-client` in the implementing PR if any
of those shapes change.

## Web surface

- [index.tsx](../../apps/web/src/routes/index.tsx) becomes adaptive on
  `households.filter(active).length` (already available via `useActiveHousehold`).
- New **Overview** presentation (single-currency + mixed-currency hero variants,
  `Arkiverade` section, `Vänner`/add-friend card) per the addendum §2.
- The focused single-book dashboard needs a home distinct from the overview for the ≥2
  case. **Recommended:** a dedicated `/hushall/$id` route reusing today's `index.tsx`
  layout with a back-to-overview affordance (addendum §5.1). Whether that is a new route
  or a "focused" state of `/` is left to the implementing PR — the design fixes the
  screens, not the routing.
- Desktop: the overview replaces the centre column of `<AppShell>`; the right rail
  (`På gång`/`Knuffar`) is active-household-scoped and hidden until a book is entered.

## Out of scope / open questions

- **The contacts model is NOT built from this spec.** Add-by-number = a blind SMS invite,
  the contact graph, and reusable-across-households contacts are decided in
  [ADR-0019](../adr/0019-contacts-by-phone-and-sms-invites.md). This spec ships only the
  entry-point affordance on the overview.
- **Drill-in routing** (dedicated route vs focused state) — decide in the implementing PR.
- **Switcher-sheet overlap:** for multi-household users the overview and the switcher
  sheet both change books; kept intentionally (the sheet is the only in-context switch
  from non-home screens). Revisit if redundant.

## Rejected alternatives (from the grill)

- **Overview-always-as-home** — taxes the single-household majority with an extra tap to a
  one-card screen every launch.
- **A separate 5th nav destination** ("Översikt"/"Hushåll") — creates two competing homes,
  buries the overview, ignores the wishlist's "borde ligga som hem?" instinct.
- **A summed global net figure** — meaningless across currencies.
