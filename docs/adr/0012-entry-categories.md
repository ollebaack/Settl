# ADR-0012: Entry categories — fixed enum, server-suggested, silently defaulted

- **Status:** accepted
- **Date:** 2026-07-13

## Context

The category-icons design addendum (`docs/design/category-icons-addendum.md`) prototypes
an entry-row icon matched from the entry title via client-side keyword matching, replacing
the first-letter glyph. Its own implementation note flags that the real app should persist
a category instead of matching on title at runtime. ADR-0007 (ledger data model) predates
this and defines no category field. Grilled 2026-07-13.

## Decision

- Add a `Category` enum to `Entry`, applying to all entry types (`expense`, `iou`,
  `recurring-post`): the addendum's 12 keyword groups (cleaning, restaurant, event,
  furniture, groceries, transport, internet, rent, music, streaming, electricity, gift)
  plus `Other`.
- The API assigns the category at creation time, computed from the entry title via a
  server-side port of the addendum's keyword map (first match wins, order matters).
  There is no category picker in the add-entry flow — creation is never blocked or
  prompted.
- The user can change the stored category afterward via the existing entry-edit path
  (`PUT /entries/{id}`), governed by the same edit-lock rule as every other field
  (ADR-0007: editable until a settlement touches the entry).
- Entry-row icon rendering keeps the unconditional IOU (`⇄`) and recurring-post (`↻`)
  glyphs regardless of stored category. Category-driven icon selection only applies to
  `expense` rows. Category on IOU/recurring entries exists for future categorized
  reporting, not for icon selection today.

## Consequences

New migration, `EntryDto`/`UpdateEntryRequest` field, and `api-client` regen in the same
PR (root rule). Existing rows are backfilled to `Other` — no retroactive keyword
matching — consistent with "silently defaulted, user corrects if wrong." Keyword-matching
logic now lives once, server-side, instead of being duplicated in every client (web today,
Expo later) — keeps ADR-0006 (business logic in the API) intact. Category stored but
never rendered on IOU/recurring entries is dead weight until spend-by-category reporting
ships; revisit if that never happens. No i18n plan — the keyword list stays Swedish-only,
matching the rest of the app today.
