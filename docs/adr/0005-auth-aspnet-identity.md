# ADR-0005: Self-hosted auth — ASP.NET Identity, cookie sessions, invite-gated households, Resend email

- **Status:** accepted
- **Date:** 2026-07-12 (session mechanism / invites / email provider added 2026-07-13, was ADR-0011; native bearer scheme resolved 2026-07-18)

## Context

Settl needs accounts, household invites, and (later) token auth for a mobile app.
Hosted auth (Clerk/Auth0) would be faster to good UX; self-hosting keeps zero vendors
and uses existing .NET knowledge. This ADR fixes the auth foundation. It also absorbs the
follow-up grill (**ADR-0011**, 2026-07-13) that settled the session mechanism, invite
flow, and email provider — details this ADR originally deferred — so that
`ICurrentUserAccessor`'s dev-header stand-in (tech-debt/0003) can eventually be replaced
with real auth.

## Decision

- **Self-hosted ASP.NET Identity**, with a token flow the future Expo app can use — no
  hosted-auth vendor, no per-user pricing.
- **Cookie authentication for the web SPA**; the **native client** (Expo, `docs/specs/mobile-app.md`)
  uses **opaque bearer tokens** — the ASP.NET Core Identity `BearerToken` handler, the same
  tokens `MapIdentityApi` issues — alongside the cookie scheme. Direct email/password login at
  `POST /auth/token`; a short-lived access token plus a ~30-day sliding refresh, rotated on use
  at `POST /auth/token/refresh`, stored in the iOS Keychain. Opaque (not a JWT) is fine — only
  this API validates them; full OAuth2/OIDC + PKCE was rejected as overkill for one first-party
  client whose direct-login flow has no auth code to protect. Resolved 2026-07-18.
- **Signup is open** (email + password creates a standalone account); **joining a
  household is invite-only**. An invite creates a pending `Member`/`HouseholdMembership`
  that activates when the invitee follows an emailed link and sets their own password —
  no credentials are ever emailed.
- **Any existing household member can send an invite**; `HouseholdMembership` stays the
  plain many-to-many ADR-0007 fixed, with no role field.
- **Resend** sends invite and password-reset email.

## Consequences

We own password reset, invite links, and therefore **email delivery** — a real cost
accepted knowingly, resolved by the Resend choice (this closes tech-debt/0001 even though
sending code came later). Cookie auth needs CSRF middleware on state-changing endpoints,
and it doesn't extend to a native client — so the Expo app adds a second (bearer-token) auth
scheme maintained alongside the cookie, now built (2026-07-18). Revocation rides Identity's
security stamp, re-checked on refresh (logout / password-change invalidate all refresh tokens);
per-token reuse-detection is deliberately not implemented (tech-debt/0012). Open signup means an unauthenticated
registration endpoint exists (rate-limiting/spam is our problem), but nobody joins a
household without an explicit invite, so the shared-ledger data stays gated. There's no
role/owner concept on membership, so any member — including one invited five minutes ago
— can invite further members; revisit if that needs gatekeeping later. Resend is a new
vendor dependency, justified by deliverability (SPF/DKIM, bounce handling) we'd otherwise
build by hand for near-zero volume; its free tier carries no meaningful cost today.
tech-debt/0003 (dev user switcher) is unaffected until the feature is built:
`ICurrentUserAccessor` still resolves from the `X-Settl-User` header until then.

## Sources

- ADR-0007: Ledger data model (Member/Household many-to-many, unchanged)
- tech-debt/0001: No email delivery story (resolved by the Resend choice)
- tech-debt/0003: Dev-only current-user switcher (paid down when this ADR is implemented)
- tech-debt/0012: Native refresh tokens have no reuse-detection (accepted with the bearer scheme)
- docs/specs/mobile-app.md: the native client that consumes the bearer scheme (grill, 2026-07-18)
