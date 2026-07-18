# Avatar personalization (emoji, no image uploads) — spec

Let members personalize their avatar with an optional **emoji** shown on their existing
email-derived `AvatarColor`, keeping the letter initial as the fallback. Settl stores no
user-uploaded images. This answers the profile wishlist item *"byta profilbild"* without
building object storage, an upload pipeline, or a moderation surface. Visual reference:
[profile-addendum.md](../design/profile-addendum.md) (and `docs/design/Settl Profile.dc.html`).

Provenance: decided via `/grill` on 2026-07-16. No companion ADR — the grill concluded
this is "cheaply reversible… a product+infra *boundary*, not a one-way door," which by
`docs/README.md`'s bar ("if it can be undone in an afternoon, it's not an ADR") makes it
a spec. The no-image-uploads boundary is recorded here rather than as a standalone
decision. Originally drafted as "ADR-0019: Avatar personalization via emoji, no image
uploads".

## Problem

An avatar today is a generated `Member.AvatarColor` (hex, derived from email) plus a
letter initial — there is no image field, no upload endpoint (`apps/api` has no
`IFormFile` or multipart handling), and no app-level object storage (R2 is wired for
Postgres backups only, [ADR-0015](../adr/0015-postgres-backups-to-cloudflare-r2.md)).
Real photo uploads would require new infrastructure against a single-container VPS deploy
with an ephemeral filesystem ([ADR-0014](../adr/0014-deploy-via-dokploy.md)):
storage or a DB blob, a resize/re-encode + EXIF-strip pipeline, a moderation stance, and
backups. A household is 2–6 people who invite each other and already know each other by
name.

## Scope (v1)

- **No user-uploaded images.** Personalization is an optional emoji rendered on the
  existing `AvatarColor`; the letter initial stays as the fallback when unset.
- **One new nullable field on `Member`** (`AvatarEmoji`), settable from the profile page,
  clearable back to the initial. `AvatarColor` stays deterministic from email — no color
  picker.
- **The emoji is untrusted text.** Validate server-side that it is a single emoji
  grapheme and cap its length; it renders in *other* members' UIs (React escapes by
  default, so the risk is display/DoS, not injection).
- The field propagates to every DTO that already carries `AvatarColor` so avatars render
  consistently across Home/Ledger/detail, not just the profile page.
- **No** S3 SDK, new credential, mounted volume, or upload/multipart endpoint.

## Data model

`Member` gains a nullable `AvatarEmoji` string column (new EF migration). It rides the
existing `pg_dump`→R2 backup for free — no new infrastructure. No backfill needed
(null = "use the initial").

## API surface

- `MeDto` gains `AvatarEmoji` (nullable) — and `Email` for the profile page's read-only
  account row (not currently on `MeDto`; see
  [profile-addendum.md](../design/profile-addendum.md) §2.1).
- `MemberDto` and the entry/household member DTOs (`HouseholdDtos.cs`, `EntryDtos.cs`)
  that carry `AvatarColor` also carry `AvatarEmoji`.
- Set via the profile endpoint: `PUT /me { name, avatarEmoji }` (`null`/empty → reset to
  initial). Server-side single-grapheme + length validation is authoritative (ADR-0006).
- Regenerate `packages/api-client` in the implementing PR (root rule).

## Web surface

- Extend [member-avatar.tsx](../../apps/web/src/components/member-avatar.tsx) to render
  `avatarEmoji` centered on the color when set, else the letter initial — everywhere
  avatars appear.
- Profile page + avatar picker sheet per
  [profile-addendum.md](../design/profile-addendum.md) §2.1–2.2 (`Välj profilbild`, emoji
  grid, "use my letter instead" reset).

## Out of scope / open questions

- **Real photo uploads** — deferred; would be a future decision (leading candidate: a
  Postgres `bytea` thumbnail, no new infra, included in existing backups).
- **Email change** — the account row is display-only; changing email needs its own
  re-verification decision.
- **Emoji picker dependency** — a no-dependency curated grid vs a searchable picker
  library (`frimousse` / `emoji-picker-element`) is an open call under the root
  no-new-deps rule; state the why before adding a package
  ([profile-addendum.md](../design/profile-addendum.md) §4.4).

## Rejected alternatives (from the grill)

- **Uploaded photos** — full storage + resize + moderation + backup build for a small
  trusted group.
- **Gravatar / user-supplied URL** — external dependency, leaks email hashes to a third
  party, invites SSRF/hotlink/broken-image problems.
- **A curated shipped icon set** — design + maintenance of a closed list the emoji picker
  gives us for free.
- **A color picker** — breaks the email-deterministic `AvatarColor` for no clear gain.
