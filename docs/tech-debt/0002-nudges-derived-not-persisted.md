# 0002: Nudges are derived on read, not persisted

**Status: PAID DOWN (2026-07-17).** The persisted, de-duplicated record this entry
called for now exists as the **emitted-nudge log** introduced by the reminder-delivery
feature (`docs/specs/reminder-delivery.md`, ADR-0024): one row per (member, nudge
identity, sent instant), keyed off each nudge's derivable identity, used to email each
nudge at most once. See `apps/api/Settl.Api/Domain/EmittedNudge` and
`Services/NudgeDigestService.cs`. Kept (not deleted) because ADR-0023 and ADR-0024 cite
this entry as a source.

**What:** The nudge feed is computed from live state per request (ADR-0007). No
notification records, so no read/unread, no dedup memory, no push capability.

**Why we took it:** The design has no read/unread UI, and persisted notifications
need dedup/expiry logic the product doesn't ask for yet.

**Trigger to pay it down:** Out-of-app delivery — reached by ADR-0024 (email-first
nudge delivery). That feature introduced the delivered-notification records. Note the
handoff shifted from "push later" to "email first": the delivery record is a
delivery-dedup log keyed by derivable nudge identity, **not** a mirror of nudge state,
because ADR-0023 derives balance crossings on read with no shared state to persist.
