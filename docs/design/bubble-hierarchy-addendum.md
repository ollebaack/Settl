# Bubble hierarchy — household · person · you

Reference: [`Settl Bubble Hierarchy.dc.html`](./Settl%20Bubble%20Hierarchy.dc.html).
Addresses wishlist #5 — "Grupp-bubblorna och profil-bubblan smälter ihop lite för
mycket nu." Pure presentation; **no API-shape change**, so `packages/api-client`
is untouched. Avatar colour is already member data (`avatarColor`).

## The rule

**Circles are people. Rounded squares are things** (households, and by extension
recurring costs). Applied everywhere the two meet: sidebar, mobile top header,
household switcher, Home per-person rows, `MemberAvatarStack`, account bubble.

| Type | Shape | Fill | Initial | Marker |
| --- | --- | --- | --- | --- |
| **Household** | rounded square (~⅓ radius) | `--soft` (brand mint), hairline `rgba(46,125,91,.18)` | `--accent` green | — same for every household, **never** a member colour |
| **Person** | circle | the member's own `avatarColor` | luminance-derived ink | — |
| **You** | circle (a person) | your own `avatarColor` | ink | thin `--accent` ring |

Why it un-blends: the shapes were already nominally distinct (person =
`rounded-full`; household badge already a soft-mint square via `bg-accent` +
`text-accent-foreground` — in the app's tokens `--accent` *is* the soft mint and
`--accent-foreground` the green, the inverse of the design `.dc.html` vocabulary).
The actual gaps were (1) **no marker for "you"** — the profile bubble was just
another circle — and (2) **shape drift**: the switcher badge used `rounded-xl`
(~44% radius at 36px), round enough to read as a circle. The fix reinforces the
square (consistent ~⅓ radius + a hairline so a household reads as a *surface*, not
an avatar) and adds a thin accent ring to mark "you" where no "Du" label exists
(account bubble, avatar stack).

Ring reuse is deliberate and non-conflicting: **`--accent` ring = "the current
one"** — around a person **circle** it means *you*; around a household **card** (the
switcher's active book) it means *the active household*. The shape disambiguates.

## What was implemented

- **`apps/web/src/components/household-badge.tsx`** *(new)* — `HouseholdBadge`
  (sizes `sm|md|lg`): a rounded **square** (`rounded-[8px]/[11px]/[14px]` ≈ ⅓
  radius) on the soft surface `bg-accent` + `text-accent-foreground` with a
  hairline `border-accent-foreground/15`. Parallels `MemberAvatar` — the shape
  encodes "thing" vs "person". Same brand tint for every household; never a
  member colour.
- **`apps/web/src/components/member-avatar.tsx`** — `MemberAvatar` gained an
  `isYou` prop → `ring-2 ring-primary ring-offset-2 ring-offset-background`.
  `MemberAvatarStack` takes `isYou` per member and rings the acting user (`z-10`
  so the ring stays whole over the overlap).
- **`apps/web/src/components/app-shell.tsx`** — removed the inline `HH_BADGE`
  constant; sidebar + mobile-header triggers now render `<HouseholdBadge>`. The
  mobile-header stack computes `isYou` via `m.id === me?.id` (`useMe`).
- **`apps/web/src/components/sheets/household-switcher-sheet.tsx`** — list badge →
  `<HouseholdBadge size="md">` (was `rounded-xl`, too round). Active-book
  `ring-primary` stays on the **card**, not the badge.
- **`apps/web/src/components/account-menu.tsx`** — the trigger `MemberAvatar` (the
  acting user) passes `isYou`, so the profile bubble wears the accent ring.

Not touched: Home per-person rows (`routes/index.tsx`) show **counterparties**,
not you, so no ring there. Recurring/category badges stay rounded-square icon
tiles — they already fit "squares are things".

Tokens: **semantic classes only** (no hex), per `apps/web/CLAUDE.md`. Verified live
against the seeded stack (light/dark): household = 8/11px square + hairline, person
= circle in own colour, "you" (Du) = circle + accent ring in every context.
