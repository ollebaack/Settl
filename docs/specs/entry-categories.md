# Entry categories — spec

Replaces the first-letter avatar on expense rows with a meaningful icon (groceries,
restaurant, rent, …), backed by a stored `category` on `Entry` instead of runtime title
matching. Visual reference: `docs/design/category-icons-addendum.md`. Data-model decision:
[ADR-0012](../adr/0012-entry-categories.md).

## Problem

[entry-row.tsx](../../apps/web/src/components/entry-row.tsx) renders `entry.title.trim()[0]`
as the glyph for expense rows today. The design addendum prototypes a nicer icon matched
by title keywords, client-side, but flags that as a placeholder for a real stored field.

## Scope (v1)

- New `Category` enum on `Entry` (API), covering all entry types, but only `expense` rows
  use it for icon selection — IOU keeps `⇄`, recurring-post keeps `↻`, unconditionally.
- Category is assigned server-side at creation, from the title, via the keyword map below.
  **No picker in the add-entry flow** — nothing blocks or prompts at creation time.
- User can change the category afterward from the entry detail sheet. Reuses the existing
  edit path (`PUT /entries/{id}` in
  [EntriesEndpoints.cs:97](../../apps/api/Settl.Api/Features/EntriesEndpoints.cs#L97)),
  which already 409s if a settlement has touched the entry (ADR-0007 lock) — no new lock
  logic needed.
- Migration backfills existing rows to `Other` (no retroactive keyword matching).

## Data model

`Category` enum (13 values — 12 keyword groups + fallback), mirroring the addendum:

| Category | Icon (lucide) | Keywords (Swedish, first match wins — order matters) |
| --- | --- | --- |
| `Cleaning` | `Sparkles` | städ, rengör, tvätt |
| `Restaurant` | `Utensils` | takeaway, thai, pizza, sushi, restaurang, lunch, middag, käk |
| `Event` | `Ticket` | konsert, biljett, bio, match, event |
| `Furniture` | `Armchair` | soffa, möbel, stol, bord, säng, fåtölj |
| `Groceries` | `ShoppingCart` | mat, handl, ica, coop, willys, lidl, hemköp |
| `Transport` | `Car` | taxi, buss, tåg, resa, bensin, parkering |
| `Internet` | `Wifi` | internet, wifi, bredband |
| `Rent` | `Home` | hyra |
| `Music` | `Music` | spotify, musik |
| `Streaming` | `Tv` | netflix, hbo, stream, film, tv |
| `Electricity` | `Zap` | "el ", elräkning, ström |
| `Gift` | `Gift` | blommor, present, gåva, årsdag |
| `Other` | `ReceiptText` | fallback — no keyword matched |

`Cleaning` is checked before `Groceries` so "Städmaterial" doesn't match groceries (same
ordering caveat as the addendum).

## API surface

- `Entry.Category` (new enum column, non-null, defaults `Other`).
- `EntryDto` gains `category: CategoryName`.
- `CreateEntryRequest` / `CreateRecurringRequest` do **not** gain a category field — the
  API computes it from `Title` at creation
  ([EntriesEndpoints.cs:52](../../apps/api/Settl.Api/Features/EntriesEndpoints.cs#L52),
  `RecurringEndpoints.cs`), same keyword function used for the template and each posted
  cycle.
- `UpdateEntryRequest` gains an optional `category` so the user can override it later
  through the existing `PUT /entries/{id}` path.
- Regenerate `packages/api-client` in the same PR (root rule).

## Web surface

- [entry-row.tsx](../../apps/web/src/components/entry-row.tsx) `Glyph`: for
  `type === 'expense'`, render the lucide icon for `entry.category` instead of the first
  letter. IOU/recurring branches unchanged.
- [entry-detail-sheet.tsx](../../apps/web/src/components/sheets/entry-detail-sheet.tsx):
  add a small tap-to-change affordance on the category icon (e.g. opens a `Select`/menu of
  the 13 values) for `expense` entries, wired through the existing `useUpdateEntry`-style
  mutation pattern. Not shown for IOU/recurring (their glyph doesn't reflect category).
- [add-entry-sheet.tsx](../../apps/web/src/components/sheets/add-entry-sheet.tsx): no
  changes — category is never chosen at creation time.

## Out of scope / open questions

- No per-household custom categories — fixed enum only (ADR-0012).
- No spend-by-category reporting yet — this spec only lands the field and the icon;
  category on IOU/recurring entries is unused until a reporting feature reads it.
- No i18n — keyword list and category labels stay Swedish-only, matching the rest of the
  app.
