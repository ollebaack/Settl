# ADR-0011: Cookie sessions, invite-gated households, Resend for email

- **Status:** accepted
- **Date:** 2026-07-13

## Context

ADR-0005 fixed self-hosted ASP.NET Identity but explicitly deferred the session
mechanism, invite flow, and email provider to a dedicated grill session before the auth
feature is built. tech-debt/0001 tied the email provider choice to "the moment household
invites are designed" — that moment is now. This ADR settles those details so
`ICurrentUserAccessor`'s dev-header stand-in (tech-debt/0003) can eventually be replaced
with real auth.

## Decision

We will use ASP.NET Identity's cookie authentication for the web SPA now, adding a JWT
bearer scheme alongside it only when the Expo app is actually built — not before. Signup
is open (email + password creates a standalone account); joining a household is
invite-only. An invite creates a pending `Member`/`HouseholdMembership` that activates
when the invitee follows an emailed link and sets their own password — no credentials
are ever emailed. Any existing household member can send an invite; `HouseholdMembership`
stays the plain many-to-many ADR-0007 fixed, with no role field. Resend sends invite and
password-reset email.

## Consequences

Cookie auth needs CSRF middleware on state-changing endpoints, and it doesn't extend to
a native client — when the Expo app ships, a second (JWT) auth scheme has to be built and
maintained alongside this one. That's deferred complexity accepted knowingly rather than
built speculatively today. Open signup means an unauthenticated registration endpoint
exists (rate-limiting/spam is our problem), but nobody joins a household without an
explicit invite, so the actual shared-ledger data stays gated. There's no role/owner
concept on household membership, so any member — including one invited five minutes ago
— can invite further members; revisit if that needs gatekeeping later. Resend is a new
vendor dependency, justified by deliverability (SPF/DKIM, bounce handling) we'd otherwise
build by hand for near-zero email volume; its free tier covers Settl's expected volume, so
it carries no meaningful cost today.

This resolves tech-debt/0001 (email delivery story) — the decision is made even though
sending code doesn't exist yet. tech-debt/0003 (dev user switcher) is unaffected until the
feature described here is actually built: `ICurrentUserAccessor` still resolves from the
`X-Settl-User` header until that work happens.

## Sources

- ADR-0005: Self-hosted auth with ASP.NET Identity (implementation details fixed here)
- ADR-0007: Ledger data model (Member/Household many-to-many, unchanged)
- tech-debt/0001: No email delivery story (resolved by the Resend choice)
- tech-debt/0003: Dev-only current-user switcher (paid down when this ADR is implemented)
