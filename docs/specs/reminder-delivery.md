# Reminder delivery — spec

Turn the app's derived nudges into real reminders that reach people outside the app, by
email. This makes the product brief's headline differentiator ("smart, event-triggered
reminders") actually deliver, without breaking its "never nagging" promise. The
load-bearing decisions — channel (email-first, defer push) and balance-crossing detection —
are in the [Decision record](#decision-record-adr-0023-adr-0024) below (ADR-0023, ADR-0024).
Email vendor is Resend ([ADR-0005](../adr/0005-auth-aspnet-identity.md)); nudge triggers are
defined by ADR-0007 and live in `apps/api/Settl.Api/Domain/NudgeCalculator.cs`.

Provenance: decided via `/grill` on 2026-07-17. The channel + crossing decisions are
recorded below; this spec owns the feature shape (scheduler, cadence, consent, and the
emitted-nudge log). This feature is the pay-down trigger for
[tech-debt/0002](../tech-debt/0002-nudges-derived-not-persisted.md) — the persisted,
de-duplicated notification records ADR-0023 deferred until out-of-app delivery existed.

## Problem

Nudges exist only as an on-read projection: `NudgeCalculator` derives them per request and
`Features/NudgesEndpoints.cs` serves them to the SPA, which renders them on the Activity /
Knuffar screen. Nothing is ever sent — a user who doesn't open the app never learns that
rent posts in 3 days or that a balance crossed the threshold. The brief's central promise
is undelivered.

## Scope (v1)

- **Channel:** email only, sent through the existing Resend sender
  (`apps/api/Settl.Api/Services/IEmailSender.cs`). No web push, no native push (ADR-0024).
- **Cadence:** a **daily digest**, at most **one email per user per day**, sent only when
  that user has ≥1 pending nudge. Silent when there's nothing to say. No per-event emails
  in v1.
- **Scheduler:** extend the existing hosted background service
  (`apps/api/Settl.Api/Services/RecurringPostingService.cs`) — do **not** add a second
  worker — to run a daily pass that, per member, computes their current nudges (reusing
  `NudgeCalculator`), diffs against the sent-log, and emails a digest of the un-sent ones.
- **Dedup (load-bearing):** a persistent condition (e.g. "you owe Sam > 750 kr") must be
  emailed **once**, not re-sent every day it stays true — otherwise the digest itself nags.
  Two mechanisms already make this tractable, both shipped in
  ADR-0023 (see [Decision record](#decision-record-adr-0023-adr-0024)): (1) the balance nudge now
  only fires on a *fresh crossing* within a 7-day window, so a standing debt goes quiet on
  its own; (2) every nudge carries a **derivable identity** (see the sent-log below). So
  dedup is a plain "have we already emailed this identity?" check against the emitted-nudge
  log — **no shared mutable crossing-state to coordinate with**. ADR-0023 derives crossings
  on read and explicitly left this persisted, de-duplicated log to *this* feature
  (tech-debt/0002), so the crossing work is a **shipped prerequisite, not an open dependency**.
- **Consent:** nudge emails are **off by default** — an explicit opt-in the member turns on
  with the profile switch (the earlier gentle/direct tone toggle is gone; in-app nudges use the
  direct voice). A **one-click unsubscribe** still forces the opt-in off. Unsubscribe is
  all-or-nothing in v1 (no per-trigger toggles).
- **No** in-app notification centre, read receipts, or SMS here — email out only.

## Data model

New persistence, provider-portable EF Core (ADR-0010), money/time rules unchanged:

- **Emitted-nudge log** (the tech-debt/0002 record) — one row per (member, nudge identity,
  sent instant). "Nudge identity" is a stable key derivable entirely from the nudge's own
  fields (the `NudgeDto.Kind` + subject id already returned by `NudgeCalculator`), no new
  state needed:
  - `recurringDue:{recurringId}:{nextPostDate}` — re-fires each cycle.
  - `bigExpense:{entryId}` — one entry, one nudge.
  - `balance:{memberId}:{crossedOn}` — anchored on `NudgeCalculator.BalanceInput.CrossedOn`
    (the crossing date from ADR-0023). A settle-below-then-recross yields a new `crossedOn`,
    so it correctly counts as a new nudge; a standing balance keeps the same key and is
    emailed once.
  Because every key is derived, no coordination with the crossing code is required — the log
  is a standalone delivery-dedup record, not a mirror of nudge state.
- **Email preference on the member/user** — `nudgeEmailsEnabled` (default false; new members
  opt in via the profile switch). The `NudgeEmailsDefaultOff` migration only flips the column
  default — existing members who were already enabled keep their setting and are not
  retroactively unsubscribed.
- Digest send timestamps are UTC (project rule); the daily pass picks a fixed send hour —
  interpret it in a sensible local zone for Sweden (Europe/Stockholm) rather than raw UTC so
  digests don't land at 02:00 local. Zone handling is an implementation detail, called out
  so it isn't missed.

## API surface

- **Unsubscribe** — a tokenised `GET`/`POST` unsubscribe endpoint reachable from the email
  (no login required to turn *off*), plus the preference exposed on the authenticated
  profile/`me` surface so it's toggleable in-app. If the `me`/profile DTO shape changes,
  regenerate `packages/api-client` in the same PR (root rule).
- No change to the existing read-time `GET /households/{id}/nudges` — the SPA keeps rendering
  the same nudges; this feature only adds the outbound path.

## Web surface

- A **nudge-email preference** switch on the Profile screen (`apps/web/src/routes/profil.tsx`):
  a single on/off, off by default. The tone chooser is gone — nudge tone is fixed to the direct
  voice. No other UI — delivery is server-side.

## Out of scope / open questions

- **Web/native push** — deferred to when the Expo app exists (ADR-0024 revisit trigger).
- **Per-trigger opt-out** and an in-app notification centre — v2 if users ask.
- **Exact digest send hour** and whether it's user-configurable — pick a sane fixed default
  (e.g. 08:00 Europe/Stockholm) for v1; revisit if requested.
- **Bounce/complaint handling** beyond what Resend does for us — monitor, revisit if
  deliverability suffers.
- **Resolved:** the crossing work shipped (ADR-0023, PR #44) as derive-on-read with a 7-day
  window — no shared state model — so the emitted-nudge log keys off each nudge's derivable
  identity (see Data model) with nothing to coordinate. Left here only as the reconciliation
  record.

## Decision record (ADR-0023, ADR-0024)

### Balance nudge fires on threshold crossing (ADR-0023, grilled 2026-07-17)

Emit the balance nudge only when the pair's net **crosses** `750 kr`, detected by replaying
the pair's net chronologically over `Entry.CreatedAt` / `Settlement.SettledAt` events (no
accounting `Date`, so backdating can't spoof freshness). A nudge shows only if the most
recent upward crossing (`|net|` `< 750` → `>= 750`) was within **7 days**. **No nudge is
stored** — this upholds ADR-0007's "derived on read, no notification storage" rather than
amending it. The crossing/timeline math lives in `BalanceCalculator` (unit-tested);
`NudgeCalculator` stays pure and receives the precomputed crossing timestamp. Settling below
then re-crossing, or a sign flip through zero, re-fires naturally. *Rejected:* a high-water-mark
table per pair (forces write-on-read) and an emitted-nudge log (deferred to ADR-0024 /
tech-debt/0002, justified only once real out-of-app delivery exists).

### Email-first nudge delivery (ADR-0024, grilled 2026-07-17)

Deliver nudges by **email via Resend** for v1; **defer both web push and native push**.
Email is the only channel reaching every user — including iPhone owners in the EU, where iOS
web push is unavailable under the DMA (iOS 17.4+) — with zero install friction, reusing the
Resend integration already in place (ADR-0005). Native push depends on the long-term Expo app
plus a paid Apple Developer account, so push is revisited when that app is built, not before.
This feature takes on a scheduler and the persisted, de-duplicated **emitted-nudge log**
ADR-0023 and tech-debt/0002 deferred — paying that debt down. Delivery deepens the
single-vendor Resend dependency (free tier still fits). Revisit when the native app ships, or
Resend volume stops fitting the free tier.
