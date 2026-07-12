# 0002: Nudges are derived on read, not persisted

**What:** The nudge feed is computed from live state per request (ADR-0007). No
notification records, so no read/unread, no dedup memory, no push capability.

**Why we took it:** The design has no read/unread UI, and persisted notifications
need dedup/expiry logic the product doesn't ask for yet.

**Trigger to pay it down:** Push notifications (handoff: "push later via same event
model"). That feature must introduce delivered-notification records.
