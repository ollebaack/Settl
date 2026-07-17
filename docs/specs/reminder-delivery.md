# Reminder delivery — spec

Turn the app's derived nudges into real reminders that reach people outside the app, by
email. This makes the product brief's headline differentiator ("smart, event-triggered
reminders") actually deliver, without breaking its "never nagging" promise. Channel
choice (email-first, defer push) rests on [ADR-0024](../adr/0024-email-first-nudge-delivery.md);
email vendor is Resend
([ADR-0011](../adr/0011-auth-invite-and-email-delivery.md)); nudge triggers are defined by
ADR-0007 (with balance-crossing detection added by
[ADR-0023](../adr/0023-balance-nudge-crossing-detection.md)) and live in
`apps/api/Settl.Api/Domain/NudgeCalculator.cs`.

Provenance: decided via `/grill` on 2026-07-17. Companion ADR-0024 fixes the load-bearing
channel decision; this spec owns the feature shape (scheduler, cadence, consent, and the
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
  [ADR-0023](../adr/0023-balance-nudge-crossing-detection.md): (1) the balance nudge now
  only fires on a *fresh crossing* within a 7-day window, so a standing debt goes quiet on
  its own; (2) every nudge carries a **derivable identity** (see the sent-log below). So
  dedup is a plain "have we already emailed this identity?" check against the emitted-nudge
  log — **no shared mutable crossing-state to coordinate with**. ADR-0023 derives crossings
  on read and explicitly left this persisted, de-duplicated log to *this* feature
  (tech-debt/0002), so the crossing work is a **shipped prerequisite, not an open dependency**.
- **Consent:** nudge emails are **on by default** (account-activity, legitimate interest —
  not marketing), with a **one-click unsubscribe** and honouring the existing gentle/direct
  **tone** setting (the tone-toggle chip). Unsubscribe is all-or-nothing in v1 (no
  per-trigger toggles).
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
- **Email preference on the member/user** — `nudgeEmailsEnabled` (default true) and reuse
  the tone field from the tone-toggle work. Migration backfills existing members to enabled.
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

- A **nudge-email preference** control on the Profile screen (`apps/web/src/routes/profil.tsx`),
  next to the tone setting from the tone-toggle chip: a simple on/off plus the gentle/direct
  choice. No other UI — delivery is server-side.

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
