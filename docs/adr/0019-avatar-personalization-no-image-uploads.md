# ADR-0019: Avatar personalization via emoji, no image uploads

- **Status:** accepted
- **Date:** 2026-07-16

## Context

The profile-page wishlist asks for "byta profilbild" (change profile picture). Today an
avatar is a generated `Member.AvatarColor` (hex, derived from email) plus a letter
initial — there is no image field, no upload endpoint (`apps/api` has no `IFormFile` or
multipart handling), and no app-level object storage (R2 is wired for Postgres backups
only, ADR-0015). A household is 2–6 people who invite each other and already know each
other by name. Real photo uploads would require new infrastructure — object storage or a
DB blob, a resize/re-encode + EXIF-strip pipeline, a moderation stance, and backups —
against a single-container VPS deploy with an ephemeral filesystem (ADR-0009). Grilled
2026-07-16.

## Decision

- **Settl stores no user-uploaded images.** Personalization is an optional **emoji** shown
  on the existing `AvatarColor`; the letter initial stays as the fallback when unset.
- **Add one nullable field to `Member`** (e.g. `AvatarEmoji`), exposed on `MeDto`/
  `MemberDto` and set via the profile endpoint. `AvatarColor` stays deterministic from
  email — we do not add a color picker.
- **Treat the emoji as untrusted text**: validate server-side that it is a single emoji
  grapheme and cap its length; it is rendered in *other* members' UIs (React escapes by
  default).
- No S3 SDK, no new credential, no mounted volume, no upload/multipart endpoint.

## Consequences

Personalization ships as a tiny string column that rides the existing pg_dump→R2 backup
for free — no new infrastructure, credential, or moderation surface, and the whole
upload injection/DoS/EXIF class of risk never comes into existence. The cost: no real
photos, and emoji glyphs render slightly differently per OS (acceptable — the color and
initial still identify the member on every device).

This is cheaply reversible: it is a product+infra *boundary*, not a one-way door. If real
demand for photos appears, a future ADR revisits storage (the leading candidate was a
Postgres `bytea` thumbnail — no new infra, included in existing backups — over an
app-level R2/S3 bucket or a mounted volume).

Explicitly rejected: **uploaded photos** (full storage + resize + moderation + backup
build for a small trusted group); **Gravatar / user-supplied URL** (external dependency,
leaks email hashes to a third party, and user URLs invite SSRF/hotlink/broken-image
problems); a **curated shipped icon set** (design + maintenance of a closed list the
emoji picker gives us for free); and a **color picker** (breaks the email-deterministic
`AvatarColor` for no clear gain).
