# 0010: Entry delete undo is client-side and best-effort

**What:** Delete safety is a ~5 s client "Ångra" toast that defers the `DELETE`
(ADR-0018). There is no server soft-delete, so: a delete is invisible to other
household members, unrecoverable once the window elapses, and the pending delete is
abandoned if the deleter navigates away or closes the tab before it fires (the entry
resurrects on the next load). Fails safe — no data loss — but the undo is not durable
and not shared.

**Why we took it:** ADR-0007 and ADR-0018 keep hard delete plus the open-trust,
notebook-not-bookkeeping model. A real tombstone reintroduces the audit-trail state
the product deliberately declined, for a safety net a per-device toast mostly covers.

**Trigger to pay it down:** reports of surprise balance changes caused by another
member's delete, or a requirement for cross-user delete visibility/recovery →
introduce soft-delete records, superseding the relevant part of ADR-0007/0018.
