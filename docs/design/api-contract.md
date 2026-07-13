# Settl API contract (ADR-0007)

The binding server-side contract. Implements ADR-0007. All later phases (tests, web
UI) build against this. UIs render and call; they never compute balances, shares,
nudges, or settled state (ADR-0006).

**Money:** integer **minor units** (öre) everywhere, type `long`. Never floats in
storage or math. **Timestamps:** UTC (`DateTimeOffset`/`DateTime` UTC). **Currency:**
per household, ISO string, default `SEK`. **EF Core:** Postgres (ADR-0010),
provider-portable model. Every endpoint has `.WithName(...)`; errors are
`ProblemDetails`.

Dates that are conceptually calendar dates (entry `date`, recurring `nextPostDate`) are
stored as `DateOnly`. Event instants (`createdAt`, `settledAt`) are `DateTimeOffset` UTC.

---

## 1. Entities

### Member (a person; global identity stub — auth deferred, ADR-0005)
- `Id` Guid PK
- `Name` string (e.g. "Du", "Sam")
- `AvatarColor` string (hex, member data — NOT a UI token; e.g. `#dfe6cf`)
- Initial for avatars is derived (`Name[0]`), not stored.

### Household
- `Id` Guid PK
- `Name` string
- `Currency` string, default `"SEK"`
- `CreatedAt` DateTimeOffset UTC

### HouseholdMembership (join — user↔household MANY-TO-MANY)
- `HouseholdId` Guid FK, `MemberId` Guid FK — composite PK
- `JoinedAt` DateTimeOffset UTC
- Membership **order** within a household is the member ordering used for
  deterministic remainder distribution: order by `JoinedAt`, then `MemberId`.

### Entry
- `Id` Guid PK
- `HouseholdId` Guid FK
- `Type` enum `EntryType { Expense, Iou, RecurringPost }`
- `Title` string
- `AmountMinor` long (> 0)
- `Date` DateOnly (when it occurred / was posted)
- `CreatedAt` DateTimeOffset UTC
- `PaidByMemberId` Guid? (Expense / RecurringPost)
- `FromMemberId` Guid?, `ToMemberId` Guid? (Iou: From owes To)
- `SplitMode` enum `SplitMode { Equal, Percent, Amount, None }` (None for Iou)
- `RecurringTemplateId` Guid? (RecurringPost links back to its template)
- Nav: `Shares` collection of `EntryShare`

### EntryShare (frozen per-member share + formula input)
- `EntryId` Guid FK, `MemberId` Guid FK — composite PK
- `ShareMinor` long — **frozen** integer share (computed once at write time)
- `FormulaValue` decimal? — the per-member formula input: percent (e.g. 40) for
  Percent mode, minor units for Amount mode, null for Equal.
- Iou entries store **no** EntryShare rows (debt is fully described by From/To/Amount).

### RecurringTemplate
- `Id` Guid PK
- `HouseholdId` Guid FK
- `Title` string
- `AmountMinor` long
- `Cadence` enum `Cadence { Monthly, Biweekly, Weekly }`
- `NextPostDate` DateOnly
- `PaidByMemberId` Guid
- `SplitMode` enum SplitMode (Equal/Percent/Amount)
- `Active` bool (pausing sets false; posting stops, history kept)
- `CreatedAt` DateTimeOffset UTC
- Nav: `Shares` collection of `RecurringShare`

### RecurringShare (template split formula; frozen shares are recomputed per posted cycle)
- `RecurringTemplateId` Guid FK, `MemberId` Guid FK — composite PK
- `FormulaValue` decimal? — percent or minor units per member; null for Equal.

### Settlement (FIRST-CLASS EVENT — settled state is derived from these, never a flag)
- `Id` Guid PK
- `HouseholdId` Guid FK
- `SettledAt` DateTimeOffset UTC
- `InitiatedByMemberId` Guid (the "me" who settled)
- Nav: `Closures` collection of `SettlementClosure`

### SettlementClosure (closes ONE debt within ONE entry)
- `Id` Guid PK
- `SettlementId` Guid FK
- `EntryId` Guid FK
- `DebtorMemberId` Guid, `CreditorMemberId` Guid — the debt (Debtor owes Creditor)
- Unique index on (`EntryId`, `DebtorMemberId`, `CreditorMemberId`) — a debt can be
  closed once. Reopening deletes closures.

---

## 2. Derivation rules (pure functions — unit-tested in Phase 3)

### 2.1 Shares → debts
`Debts(entry)` returns a list of `{ Debtor, Creditor, AmountMinor }`:
- **Iou:** `[{ Debtor = From, Creditor = To, AmountMinor = amount }]`.
- **Expense / RecurringPost:** for each member `m` with `ShareMinor > 0` and
  `m != PaidBy`: `{ Debtor = m, Creditor = PaidBy, AmountMinor = share_m }`.

### 2.2 Open vs closed
A debt `{Debtor, Creditor, entry}` is **closed** iff a `SettlementClosure` row exists
for (`entry`, `Debtor`, `Creditor`) OR (`entry`, `Creditor`, `Debtor`) — closures are
direction-normalized: always store with the actual debtor/creditor, and match a debt to
a closure by the unordered pair + entry. (Store debtor/creditor as the real direction;
match on the unordered pair so reopen/settle are symmetric.)
- `OpenDebts(entry)` = `Debts(entry)` minus closed ones.
- Entry `Settled` (derived) = `Debts(entry)` is non-empty AND all are closed.
- Entry `Locked` (derived) = at least one closure references the entry. Locked entries
  reject PUT/DELETE with `409` (reopen first).

### 2.3 Net balance between the viewer (me) and member X, in a household
`NetWith(me, X) = Σ over entries in household of open debts d:`
- `+ d.AmountMinor` if `d.Debtor == X && d.Creditor == me` (X owes me)
- `- d.AmountMinor` if `d.Debtor == me && d.Creditor == X` (I owe X)

`> 0` → X owes me (I'm owed). `< 0` → I owe X. `0` → square.
Overall net = Σ over other members of `NetWith(me, X)`.

### 2.4 Viewer-relative entry status (server computes; UI renders)
Given entry + viewer `me`, `OpenDebts`:
- if `Debts` non-empty and no open debts → `{ kind: "settled" }`
- else compute `owe = Σ open d where d.Debtor==me`, `owed = Σ open d where d.Creditor==me`
  - `owe > 0` → `{ kind: "youOwe", amountMinor: owe }`
  - else `owed > 0` → `{ kind: "youAreOwed", amountMinor: owed }`
  - else if entry has some closure → `{ kind: "partiallySettled" }`
  - else → `{ kind: "notYourShare" }`

---

## 3. Splitting (freeze integer shares with deterministic remainder)

Members are taken in **household membership order** (§1 HouseholdMembership). Let
`N = member count`, `A = AmountMinor`.

### Equal
- `base = A / N` (integer div), `rem = A - base*N` (0 ≤ rem < N).
- Each member gets `base`; the **first `rem` members** (membership order) get `+1` öre.
- Sum is exactly `A`. Example: `A=100, N=3` → `[34,33,33]`. `A=864_00? ` (e.g.
  `A=86400` öre, N=3) → base 28800, rem 0 → `[28800,28800,28800]`.
  `A=100 öre? ` `A=10000, N=3`→ base 3333, rem 1 → `[3334,3333,3333]`.

### Percent (values are percentages, may be fractional e.g. 33.5)
- Validate `|Σ pct − 100| ≤ 0.5`, else `400` ProblemDetails `"Procenten måste bli 100"`.
- **Largest-remainder (Hamilton):** `raw_m = A * pct_m / 100` (exact rational).
  `floor_m = ⌊raw_m⌋`. `assigned = Σ floor_m`. `leftover = A − assigned` (0 ≤ leftover < N).
  Sort members by fractional part `raw_m − floor_m` **descending**, tie-break by
  membership order; give `+1` öre to the first `leftover` members. Sum is exactly `A`.
  - Compute with integer/decimal care: `raw_m = (A * pctScaledNumerator_m) / denom`.
    Use `decimal` for the fractional-part comparison; final shares are `long`.

### Amount (values are minor units per member)
- `sum = Σ vals`. Validate `|sum − A| ≤ 5` öre (UI tolerance ±0.05 kr), else `400`
  `"Delningen måste bli {A}"`.
- Frozen share = the given `vals`, then **reconcile** the small difference `A − sum`
  deterministically so shares sum to exactly `A`: distribute the difference ±1 öre at a
  time in membership order (add if positive, subtract if negative, skipping members
  whose share would go < 0).

### Iou
- No shares table. Debt = full amount From→To. `amount > 0`.

All of the above live in a **pure static class `SplitCalculator`** with no DB/time deps,
returning `IReadOnlyList<(Guid MemberId, long ShareMinor)>`. Unit-tested exhaustively in
Phase 3 (rounding, uneven splits, remainder determinism, zero-remainder, tolerance
reconcile, N=1, N=2).

---

## 4. Recurrence engine (pure logic + hosted service)

### 4.1 Pure functions (`RecurrenceCalculator`)
- `Advance(date, cadence)`: Monthly → `date.AddMonths(1)`; Biweekly → `AddDays(14)`;
  Weekly → `AddDays(7)`.
- `CycleLengthDays(cadence)`: Monthly 30, Biweekly 14, Weekly 7 (for progress only).
- `CycleProgress(nextPostDate, cadence, today)`:
  `daysLeft = (nextPostDate - today).Days` (may be <0);
  `clamp(1 - daysLeft / CycleLengthDays, 0.04, 1.0)` when active, else 0.
- `MonthlyNormalizedMinor(amount, cadence)`: Monthly ×1, Biweekly ×2, Weekly ×4.
  **Used consistently for both recTotal and per-member recShare** (fixes prototype's
  biweekly-only inconsistency).
- `DuePosts(template, today)`: while `template.Active && NextPostDate <= today`, yield a
  post for `NextPostDate`, then `NextPostDate = Advance(NextPostDate, cadence)`.
  Deterministic, terminates.

### 4.2 Posting a cycle
For each due date, create an Entry: `Type=RecurringPost`, `RecurringTemplateId=template.Id`,
`Title = "{template.Title} — {swedish month of postDate}"` (e.g. "Hyra — juli"),
`AmountMinor`, `PaidBy`, `SplitMode`, frozen `Shares` via `SplitCalculator` from the
template's current formula (`RecurringShare`), `Date = postDate`. Then advance
`NextPostDate`. **Re-splits each cycle** from the template's current formula, so editing
the split affects only future cycles.

### 4.3 Hosted background service (`RecurringPostingService : BackgroundService`)
- **Catch-up on startup:** for every active template, post all missed cycles
  (`DuePosts`) up to today, in a single scoped DB transaction. Idempotent: a post for
  `(templateId, postDate)` is created once — guard with a unique index on
  (`RecurringTemplateId`, `Date`) for RecurringPost entries, or check existence first.
- Then loop on a timer (e.g. hourly / configurable) doing the same. Pausing
  (`Active=false`) stops posting without deleting history; resuming continues from
  `NextPostDate`.
- Posting logic delegates to the pure functions so it is unit-testable without the host.

---

## 5. Nudges (derived on read — no storage, ADR-0007 / tech-debt 0002)

`GET /households/{id}/nudges?tone=gentle|direct` (default `direct`). Computed live for
the current user, in this order:
1. **Recurring due:** each active template with `daysUntil(NextPostDate) ≤ 5`
   (and `≥ 0`). Copy per §Nudges in implementation-map.
2. **Big expense:** each Expense entry (not Iou, not RecurringPost — matches prototype
   `type !== 'iou'` but exclude auto-posts too? prototype includes recurring posts via
   `type!=='iou'`. **Decision:** include Expense AND RecurringPost with `type != iou`,
   unsettled, `AmountMinor ≥ 150000` (1500 kr), `date` within last 7 days
   (`daysUntil(date) ≥ -7`).) Action `Visa`; plus `Gör upp` when `PaidBy != me`.
3. **Balance:** each other member with `|NetWith(me, X)| ≥ 75000` (750 kr). Action
   `Gör upp`. (v1 uses `abs(net) ≥ 750`; true "crossing" needs history — see
   needs-grilling.)

Each nudge DTO: `{ kind, title, body, when, actions: [{ label, kind, targetId }] }`
where `actions[].kind ∈ { viewEntry, viewRecurring, settle }` and `targetId` is the
entry / recurring / member id the web maps to a route+sheet. `tone` only changes copy.

---

## 6. Current user (dev-only switcher behind a clean abstraction)

Auth deferred (ADR-0005). `ICurrentUserAccessor.MemberId` resolves the acting member:
- From request header `X-Settl-User: {memberId}` when present and valid,
- else a configured default (first seeded member, "Du").
Implemented as a scoped service reading `IHttpContextAccessor`. This is the ONLY place
"who am I" is decided — swap for real auth later. **Tech-debt entry required.**

Endpoints:
- `GET /me` → current member `{ id, name, avatarColor }`.
- `GET /dev/users` → all members (dev switcher list). Gated: only meaningful in dev.

---

## 7. Endpoints (all `.WithName`, ProblemDetails errors, minor-unit money)

Money fields are `long` minor units, suffixed `Minor` where ambiguous.

### Households
- `GET /households` `GetHouseholds` → for current user:
  `[{ id, name, currency, memberNames: string[], netMinor, netLabel, active }]`
  (`active` = matches current active household; if we don't persist "active", omit and
  let the client hold it — **Decision:** client holds active household id; `active`
  reflects a `?active={id}` echo or is omitted. Keep it simple: omit `active`, client
  tracks selection.)
- `POST /households` `CreateHousehold` `{ name, currency?, memberIds?: Guid[], newMemberNames?: string[] }`
  → 201 household. The acting user is always a member (added server-side, never
  listed); `memberIds` references existing members, `newMemberNames` creates fresh
  ones inline with a generated avatar colour — no invite step (auth deferred, ADR-0005).
- `GET /households/{id}` `GetHousehold` → `{ id, name, currency, members: [{id,name,avatarColor}] }`.
- `GET /households/{id}/members` `GetHouseholdMembers` → `[{ id, name, avatarColor }]`.
- `GET /households/{id}/summary` `GetHouseholdSummary` →
  `{ overallNetMinor, netLabel: "square"|"owed"|"owe", openCount,
     people: [{ memberId, name, avatarColor, netMinor, relation: "owesYou"|"youOwe"|"square" }],
     upcoming: [{ recurringId, title, nextPostDate, daysUntil, yourShareMinor, amountMinor }] }`
  (upcoming = active templates with `daysUntil ≤ 30`, soonest first, max 4.)

### Entries
- `GET /households/{id}/entries?type=&limit=&sort=date_desc` `GetEntries` →
  `[EntryDto]` (see §8). `type ∈ {expense,iou,recurring}` filters (recurring →
  RecurringPost). Default sort date desc.
- `POST /households/{id}/entries` `CreateEntry` (expense or iou):
  `{ type: "expense"|"iou", title?, amountMinor, date?, paidByMemberId?,
     fromMemberId?, toMemberId?, split?: { mode, values? } }`
  → 201 `EntryDto`. Freezes shares. Default title: iou→"Lån", else "Utan titel".
  Default date = today (UTC). Validation → 400 ProblemDetails with the exact Swedish
  `detail` string (§3).
- `GET /entries/{id}` `GetEntry` → `EntryDto` (full).
- `PUT /entries/{id}` `UpdateEntry` → 200 / **409** if locked.
- `DELETE /entries/{id}` `DeleteEntry` → 204 / **409** if locked.
- `POST /entries/{id}/settlements` `SettleEntry` → closes ALL open debts of the entry
  in one Settlement (InitiatedBy = me). 200 `EntryDto`.
- `DELETE /entries/{id}/settlements` `ReopenEntry` → deletes all closures referencing
  the entry (and empty settlements). 200 `EntryDto`.

### Recurring
- `GET /households/{id}/recurring` `GetRecurringTemplates` →
  `{ recTotalMinor, recShareMinor, templates: [RecurringDto] }` where RecurringDto:
  `{ id, title, amountMinor, cadence, nextPostDate, daysUntil, active, payerName,
     splitMode, yourShareMinor, monthlyNormalizedMinor, cycleProgress (0..1),
     contributingMemberIds: Guid[] }`.
- `POST /households/{id}/recurring` `CreateRecurringTemplate`
  `{ title?, amountMinor, cadence, nextPostDate, paidByMemberId, split: {mode,values?} }`
  → 201 RecurringDto. Does NOT post immediately; the service posts when `nextPostDate`
  arrives (or on catch-up if in the past).
- `GET /recurring/{id}` `GetRecurringTemplate` →
  `{ ...RecurringDto, shares: [{ memberId, name, shareMinor, isPayer }],
     postedEntries: [{ id, title, amountMinor, settled }] }`.
- `PATCH /recurring/{id}` `UpdateRecurringTemplate` `{ active?, title?, amountMinor?,
     cadence?, nextPostDate?, paidByMemberId?, split? }` → 200. `active` toggles
     pause/resume.

### Settlements
- `GET /households/{id}/settle-preview?person={memberId}` `GetSettlePreview` →
  `{ netMinor, netLabel: "owesYou"|"youOwe"|"square", memberName,
     entries: [{ id, title, date, signedAmountMinor }] }`
  where `signedAmountMinor > 0` means X owes me for that entry, `< 0` I owe X.
- `POST /households/{id}/settlements` `CreateSettlement` `{ personMemberId }` →
  one Settlement closing all open debts between me and person across the household.
  201 `{ settlementId }`.

### Nudges
- `GET /households/{id}/nudges?tone=` `GetNudges` → `[NudgeDto]` (§5).

### Meta
- `GET /me` `GetCurrentUser`, `GET /dev/users` `GetDevUsers`, existing `GET /health`.

---

## 8. EntryDto (shared shape)
```
{
  id, householdId, type: "expense"|"iou"|"recurringPost",
  title, amountMinor, date, createdAt,
  paidByMemberId?, fromMemberId?, toMemberId?,
  splitMode: "equal"|"percent"|"amount"|"none",
  shares: [{ memberId, name, avatarColor, shareMinor, isPayer }],   // [] for iou
  recurringTemplateId?, templateTitle?,
  settled: bool,          // derived
  locked: bool,           // derived
  viewerStatus: { kind: "settled"|"youOwe"|"youAreOwed"|"partiallySettled"|"notYourShare",
                  amountMinor }   // relative to current user (§2.4)
}
```
Iou detail rows are rendered by the web from from/to/amount; `shares` stays `[]`.

---

## 9. Seeding (dev)
A `DbInitializer` seeds when the DB is empty (dev only), matching the canonical export:
- Members: Du (`#dfe6cf`), Sam (`#f0dcc3`), Priya (`#d9e0ee`), Mamma (`#eed9d9`),
  Pappa (`#d9eee4`).
- Households: **Lönnvägen 3** (Du, Sam, Priya) SEK; **Familjen** (Du, Mamma, Pappa) SEK.
- Entries & recurring templates mirror the export fixture (amounts × 100 → öre), with
  an **active rent split ("Hyra", monthly, amount split 9000/8000/7000 kr) whose
  `nextPostDate` is within 5 days of today** so a recurring-due nudge fires. Include a
  ≥1500 kr expense and a pair balance ≥750 kr so all three nudge types have data.
- Current user default = Du.
- Seed dates are relative to `DateTime.UtcNow` (do NOT hardwire 2026-07-12), so nudges
  and cycle progress stay live.

## 10. Project layout (apps/api/Settl.Api)
- `Domain/` — entities + enums + pure `SplitCalculator`, `RecurrenceCalculator`,
  `BalanceCalculator`, `NudgeCalculator` (no EF/HTTP deps).
- `Data/` — `SettlDbContext`, entity configs, `DbInitializer`.
- `Features/` — endpoint groups (`Households`, `Entries`, `Recurring`, `Settlements`,
  `Nudges`, `Meta`) as `MapXxxEndpoints(this IEndpointRouteBuilder)` extensions.
- `Services/` — `ICurrentUserAccessor`, `RecurringPostingService`.
- `Dtos/` — request/response records.
- Packages: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`
  (ADR-0010). Keep the model provider-portable regardless.
- Migrations under `Migrations/`. Bump the `dotnet-ef` tool to 10.x (local manifest).
```
