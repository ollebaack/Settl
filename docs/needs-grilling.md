# Needs grilling

Decisions made during the full-design build that smell ADR-worthy. Each has an **interim
default** already in the code; none has been grilled, so no ADR exists yet. Run `/grill`
before treating any as settled.

| # | Decision | Interim default (in code) | Why it needs a grill |
|---|---|---|---|
| 1 | **Balance-nudge semantics** | `abs(net) ≥ 750 kr` evaluated on read | ADR-0007 / handoff say the trigger is a balance *crossing* 750 kr (an event). True crossing needs prior-state history, which the derive-on-read model (tech-debt 0002) deliberately doesn't keep. Confirm whether "crossing" is worth persisting state for, or the threshold reading is the product. |
| 2 | **Recurring resume policy** | Resume fast-forwards `nextPostDate` to the next cycle ≥ today (paused gap is skipped) | Alternative is back-posting every missed cycle on resume. Design copy ("Återuppta för att schemalägga nästa period") points at skip, but this is a real product/ledger-semantics choice — and it interacts with server-downtime catch-up, which still back-posts for *always-active* templates. |
| 3 | **Monthly cadence drift** | `DateOnly.AddMonths(1)` chained off the last post date | A template first posted on the 31st drifts earlier permanently after passing through a short month (31→Feb 28→Mar 28…). Anchoring to an intended day-of-month (clamp only for short months) is the alternative. Matters for rent/subscriptions billed on 29–31. |
| 4 | **Amount-mode PATCH ergonomics** | Changing an amount-mode template's amount without re-supplying the split → 400 (client must resend shares) | Safe (prevents the wedge bug), but the UX could instead auto-rescale the öre proportionally. Decide the intended editing model for fixed-amount splits. |
| 5 | **Theme scope** | Ship one token set (Mint) with real light + dark; the Paper/Citrus named directions from the prototype are dropped | The exports show three named palettes as look demos. Confirm they're demo-only (current assumption) vs a shipped user preference. |
| 6 | **Reminder tone** | Default `direct`; no in-app toggle built | ADR-0007 says tone only changes copy. Where the setting lives (per-user? per-household?) and its default is unspecified by the design. |
| 7 | **Overlay routing** | Sheets are URL search params (`?sheet=…&id=…`) over the active route | Deep-linkable and back-button friendly, but it's an app-wide navigation convention worth a deliberate blessing. |
| 8 | **Zero-closure pair settlement** | `POST /households/{id}/settlements` always persists a Settlement even when the pair has no open debts | Minor data-quality wart (empty settlement rows). Decide whether to no-op when nothing is open (as the single-entry settle path already does). |
| 9 | **Big-expense nudge window** | Fires for unsettled ≥1500 kr entries with `daysUntil(date) ≥ −7` (no upper bound) — matches the canonical prototype | A future-dated big expense would therefore nudge. Harmless today (entries are created dated "now") but confirm the intended window is `[today−7, today]`. |
