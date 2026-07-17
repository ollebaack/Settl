# Recurring expenses with an end date — spec

Let a recurring expense (`RecurringTemplate`, "På repeat") **stop on its own** on a chosen
date, instead of running forever until someone remembers to pause it. When creating or
editing a recurring cost you can now say **"Slutar: Aldrig / Datum / Efter N gånger"**. A
fixed-term cost — a 12-month gym membership, a 6-month instalment plan, a lease that ends
in December — posts its cycles and then quietly finishes, showing up as **Avslutad** in the
list. This builds on the existing background posting job
([RecurringPostingService.cs](../../apps/api/Settl.Api/Services/RecurringPostingService.cs))
and the pause/keep-don't-delete stance of
[ADR-0018](../adr/0018-ledger-editing-affordances.md).

Provenance: decided via `/grill` on 2026-07-17. **Not ADR-worthy** — the load-bearing
modelling calls (inclusive boundary, "ended" derived from the cursor, count resolved to a
date at save) are contained feature-design choices that build on ADR-0018 rather than fixing
a new architectural principle, and each is reversible without undoing the others. The rejected
alternatives are recorded below in lieu of an ADR.

## Problem

A `RecurringTemplate` has exactly two lifecycle controls today: `Active` (pause/resume) and
delete-if-clean-else-409 ([RecurringEndpoints.cs:231](../../apps/api/Settl.Api/Features/RecurringEndpoints.cs)).
There is no way to say "this cost ends on a date". The background worker posts every cycle
where `t.Active && t.NextPostDate <= today`
([RecurringPostingService.cs:87](../../apps/api/Settl.Api/Services/RecurringPostingService.cs))
forever. Any fixed-term cost — memberships, instalment plans, term-limited rent — therefore
requires a human to remember to pause it on the right day, or it keeps charging the ledger.
`NextPostDate` doubles as the start date and the rolling cursor (it's mutated forward on every
post — [RecurringPostingService.cs:116](../../apps/api/Settl.Api/Services/RecurringPostingService.cs)),
so there is no immutable "start" or "end" stored anywhere.

## Scope (v1)

- **One termination field: `DateOnly? EndDate`** on `RecurringTemplate`. `null` = "Aldrig"
  (today's forever behaviour, the default — no back-compat migration of existing rows needed).
- **Inclusive boundary.** A cycle posts while `NextPostDate <= EndDate`; the template stops
  once the cursor passes `EndDate`. A membership starting Jan 1, monthly, with EndDate Dec 31
  posts exactly 12 times (Jan…Dec). This mirrors the existing `<= today` gate, so it's the
  same shape of comparison.
- **"Ended" is derived, never stored.** `ended = EndDate != null && NextPostDate > EndDate`.
  No new status column, no change to `Active` semantics — `Active` still means only "paused".
  The posting job stops emitting cycles naturally once the cursor passes `EndDate`.
- **Count is input sugar, resolved to a date at save.** "Efter N gånger" computes
  `EndDate = <Nth post date>` from the relevant start reference and stores **only** `EndDate`.
  We never persist a live counter. This is immune to multi-cycle catch-up (`DuePosts` can post
  several cycles in one job run) and to cadence edits — there is a single source of truth.
- **Form: one "Slutar" mode selector** — Aldrig / Datum / Efter N — mutually exclusive. `Datum`
  reveals a date input; `Efter N` reveals a number input. All three resolve to `EndDate` (or
  `null`) on submit.
- **Ended templates stay listed as "Avslutad"**, de-emphasised, consistent with ADR-0018's
  keep-don't-delete stance. No list-hiding/archive logic in v1.
- **No auto-delete, no clawback.** Reaching `EndDate` never deletes the template and never
  touches already-posted `Entry` rows — they are real ledger history. Ended-with-history
  templates linger in the list (can't be hard-deleted per ADR-0018; user pauses/keeps).

## Data model

- Add `public DateOnly? EndDate { get; set; }` to `RecurringTemplate`
  ([Entities.cs:112](../../apps/api/Settl.Api/Domain/Entities.cs)), nullable, default `null`.
- New EF Core migration adding the nullable `EndDate` column. Existing rows backfill to `null`
  = "Aldrig" — no behavioural change for current templates.
- No change to `RecurringShare`, `Entry`, or the `IX_Entry_RecurringTemplate_Date` idempotency
  index.

## API surface

- **Posting gate.** Gate cycle emission on the end date in the pure calculator rather than the
  query, so it's unit-testable without a clock/DB: `RecurrenceCalculator.DuePosts(...)`
  ([RecurrenceCalculator.cs:62](../../apps/api/Settl.Api/Domain/RecurrenceCalculator.cs)) takes
  `EndDate` and yields no date past it (inclusive). The `PostDueCycles` query
  ([RecurringPostingService.cs:87](../../apps/api/Settl.Api/Services/RecurringPostingService.cs))
  may keep its `NextPostDate <= today` filter as-is; the calculator is authoritative for the
  stop.
- **DTOs** ([RecurringDtos.cs](../../apps/api/Settl.Api/Dtos/RecurringDtos.cs)):
  - `CreateRecurringRequest` — add termination input. Preferred shape: a small tagged input
    (`EndMode` = `never | date | count` + `EndDate?` + `EndAfterCount?`) that the endpoint
    resolves to a single `DateOnly? EndDate` server-side, so the "count → date" math lives in
    the API (business logic in the API — root rule / ADR-0006), not the web app.
  - `UpdateRecurringRequest` — same termination input, all-nullable partial. Editing count
    re-derives `EndDate`; editing to "Aldrig" sets it back to `null`.
  - `RecurringDto` / `RecurringDetailDto` — add `EndDate` and a derived `Ended` (or reuse a
    computed status) so the web can render "Avslutad" and "Slutar {datum}" without recomputing.
- **Count resolution reference.** On **create**, `NextPostDate` *is* the start, so
  `EndDate` = the Nth post date = start advanced `N-1` cadence steps
  (`RecurrenceCalculator.Advance`). On **edit** (no immutable start stored), "Efter N" means
  **N cycles from the next upcoming post** (current `NextPostDate`) — i.e. "N more times from
  here". This is the accepted wrinkle; the default reads intuitively.
- **Validation.** Reject `EndDate` earlier than the next `NextPostDate` at save (would mean
  "already ended, zero future posts") with a clear message, or treat it as immediate stop —
  decide in implementation; lean toward rejecting count `< 1` and past dates.
- **Regenerate `packages/api-client`** in the same PR (API-shape change — root rule).

## Web surface

- **Create/Edit form** — the "Återkommande" tab of `AddEntrySheet`
  ([add-entry-sheet.tsx:583](../../apps/web/src/components/sheets/add-entry-sheet.tsx)),
  where "Upprepas" and "Bokförs först" live today. Add a **"Slutar"** control:
  a three-way selector (Aldrig / Datum / Efter N) that conditionally reveals a `type="date"`
  input or a number input. Extend `FormState`
  ([add-entry-sheet.tsx:104](../../apps/web/src/components/sheets/add-entry-sheet.tsx)) with the
  end mode + value; map to the request on submit.
- **List** — `recurring.tsx` ("På repeat",
  [recurring.tsx](../../apps/web/src/routes/recurring.tsx)): render ended templates as
  **Avslutad** (de-emphasised styling; the cycle-progress bar shows complete/inactive). Sort so
  active/upcoming stay on top.
- **Detail drawer** — `RecurringDetailSheet`
  ([recurring-detail-sheet.tsx](../../apps/web/src/components/sheets/recurring-detail-sheet.tsx)):
  show "Slutar {datum}" (or "Slutar aldrig") near "Bokförs" and mark the template Avslutad once
  ended. Editing routes through the same `editRecurring` form.
- Types come from the regenerated `@settl/api-client` via
  [api.ts](../../apps/web/src/lib/api.ts); hooks `useCreateRecurring` / `useUpdateRecurring`
  ([queries.ts](../../apps/web/src/lib/queries.ts)) are unchanged apart from the wider request.

## Rejected alternatives

- **Exclusive / hard-cutoff boundary** — a post landing exactly on `EndDate` would *not* post.
  Surprising: setting end = Dec 1 silently drops December. Rejected for the inclusive gate.
- **Explicit lifecycle enum (Active/Paused/Ended)** — a second source of truth that must stay
  in sync with the cursor for the same fact. Rejected; "ended" is derived.
- **Reusing `Active=false` on the final post** — conflates Avslutad with Pausad, and a user
  could "Återuppta" a finished template past its end. Rejected.
- **Live decrementing occurrence counter** — fragile against multi-cycle catch-up posting and
  cadence edits, and a second source of truth alongside the cursor. Rejected; count resolves to
  a date at save.
- **Two independent date + count fields** — precedence ambiguity when both are set. Rejected;
  one mutually-exclusive mode selector, one stored `EndDate`.

## Out of scope / open questions

- **No archive view.** Ended templates stay in the main list; a dedicated archive/filter is a
  later call if they accumulate.
- **No "X of N posted" progress.** Because count is not stored, the UI shows "Slutar {datum}",
  not "3 av 6". First-class instalment progress would need the counter we deliberately rejected.
- **Edit-count reference.** Confirm in build/QA that "Efter N" on edit meaning "N from the next
  post" reads correctly to users; if not, revisit storing an immutable `StartDate`.
- **Tests required** (root/CLAUDE rules): unit coverage for the end-date gate and count→date
  resolution in `RecurrenceTests.cs`; a Playwright spec extending
  [recurring.spec.ts](../../apps/web/e2e/recurring.spec.ts) for create-with-end-date and the
  Avslutad state.
