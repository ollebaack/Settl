using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Tests;

/// <summary>
/// Exhaustive pure-domain tests for <see cref="BalanceCalculator"/> and its helpers
/// (<see cref="ClosureLookup"/>). Entities are built in memory with frozen
/// <see cref="EntryShare"/> rows — no DB / HTTP. Assertions follow the api-contract §2.4
/// status rules and the debt/net sign conventions documented on the calculator.
/// </summary>
public class BalanceCalculatorTests
{
    // Reuse the fixed seed identities purely as convenient distinct GUIDs.
    private static readonly Guid A = SeedIds.Du;
    private static readonly Guid B = SeedIds.Sam;
    private static readonly Guid C = SeedIds.Priya;
    private static readonly Guid D = SeedIds.Mamma;

    private static Entry Expense(Guid paidBy, params (Guid member, long share)[] shares) => new()
    {
        Id = Guid.NewGuid(),
        Type = EntryType.Expense,
        AmountMinor = shares.Sum(s => s.share),
        PaidByMemberId = paidBy,
        Shares = shares
            .Select(s => new EntryShare { MemberId = s.member, ShareMinor = s.share })
            .ToList(),
    };

    private static Entry Iou(Guid? from, Guid? to, long amount) => new()
    {
        Id = Guid.NewGuid(),
        Type = EntryType.Iou,
        AmountMinor = amount,
        FromMemberId = from,
        ToMemberId = to,
    };

    private static SettlementClosure Close(Guid entryId, Guid debtor, Guid creditor) => new()
    {
        Id = Guid.NewGuid(),
        EntryId = entryId,
        DebtorMemberId = debtor,
        CreditorMemberId = creditor,
    };

    private static ClosureLookup Lookup(params SettlementClosure[] closures) => new(closures);

    private static readonly ClosureLookup None = Lookup();

    // ---- Debts: expense --------------------------------------------------

    [Fact]
    public void Debts_expense_each_nonpayer_with_positive_share_owes_the_payer()
    {
        var e = Expense(A, (A, 50), (B, 100), (C, 200));

        var debts = BalanceCalculator.Debts(e);

        // Payer's own share is excluded; only B and C owe A.
        Assert.Equal(2, debts.Count);
        Assert.Contains(new Debt(B, A, 100), debts);
        Assert.Contains(new Debt(C, A, 200), debts);
        Assert.DoesNotContain(debts, d => d.Debtor == A);
    }

    [Fact]
    public void Debts_expense_excludes_zero_share_members()
    {
        var e = Expense(A, (A, 100), (B, 0), (C, 300));

        var debts = BalanceCalculator.Debts(e);

        Assert.Single(debts);
        Assert.Equal(new Debt(C, A, 300), debts[0]);
    }

    [Fact]
    public void Debts_expense_excludes_payer_even_when_payer_has_positive_share()
    {
        var e = Expense(A, (A, 999));

        Assert.Empty(BalanceCalculator.Debts(e));
    }

    [Fact]
    public void Debts_expense_with_no_payer_is_empty()
    {
        var e = Expense(A, (B, 100));
        e.PaidByMemberId = null;

        Assert.Empty(BalanceCalculator.Debts(e));
    }

    // ---- Debts: iou ------------------------------------------------------

    [Fact]
    public void Debts_iou_is_a_single_from_to_debt()
    {
        var e = Iou(B, A, 750);

        var debts = BalanceCalculator.Debts(e);

        Assert.Single(debts);
        Assert.Equal(new Debt(B, A, 750), debts[0]);
    }

    [Fact]
    public void Debts_iou_ignores_any_shares_on_the_entry()
    {
        var e = Iou(B, A, 750);
        e.Shares = new List<EntryShare> { new() { MemberId = C, ShareMinor = 500 } };

        var debts = BalanceCalculator.Debts(e);

        Assert.Single(debts);
        Assert.Equal(new Debt(B, A, 750), debts[0]);
    }

    [Fact]
    public void Debts_iou_with_missing_endpoint_is_empty()
    {
        Assert.Empty(BalanceCalculator.Debts(Iou(null, A, 750)));
        Assert.Empty(BalanceCalculator.Debts(Iou(B, null, 750)));
    }

    // ---- ClosureLookup direction normalization ---------------------------

    [Fact]
    public void ClosureLookup_is_closed_regardless_of_query_direction()
    {
        var entryId = Guid.NewGuid();
        var lookup = Lookup(Close(entryId, A, B)); // stored debtor=A, creditor=B

        Assert.True(lookup.IsClosed(entryId, A, B));
        Assert.True(lookup.IsClosed(entryId, B, A)); // reversed order still closed
    }

    [Fact]
    public void ClosureLookup_scopes_closure_to_its_entry_and_pair()
    {
        var entryId = Guid.NewGuid();
        var otherEntry = Guid.NewGuid();
        var lookup = Lookup(Close(entryId, A, B));

        Assert.False(lookup.IsClosed(otherEntry, A, B)); // wrong entry
        Assert.False(lookup.IsClosed(entryId, A, C));    // wrong pair
        Assert.False(lookup.IsClosed(entryId, C, D));    // unrelated pair
    }

    [Fact]
    public void ClosureLookup_AnyForEntry_tracks_referenced_entries()
    {
        var entryId = Guid.NewGuid();
        var lookup = Lookup(Close(entryId, A, B));

        Assert.True(lookup.AnyForEntry(entryId));
        Assert.False(lookup.AnyForEntry(Guid.NewGuid()));
    }

    // ---- OpenDebts / IsSettled / IsLocked --------------------------------

    [Fact]
    public void OpenDebts_excludes_closed_debts_only()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        var closures = Lookup(Close(e.Id, B, A));

        var open = BalanceCalculator.OpenDebts(e, closures);

        Assert.Single(open);
        Assert.Equal(new Debt(C, A, 100), open[0]);
    }

    [Fact]
    public void IsSettled_true_only_when_debts_exist_and_all_closed()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        var allClosed = Lookup(Close(e.Id, B, A), Close(e.Id, C, A));

        Assert.True(BalanceCalculator.IsSettled(e, allClosed));
    }

    [Fact]
    public void IsSettled_false_when_partially_closed()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        var partial = Lookup(Close(e.Id, B, A));

        Assert.False(BalanceCalculator.IsSettled(e, partial));
    }

    [Fact]
    public void IsSettled_false_for_entry_with_no_debts()
    {
        // An expense fully self-paid (payer is the only positive share) has zero debts.
        var e = Expense(A, (A, 500));

        Assert.False(BalanceCalculator.IsSettled(e, None));
    }

    [Fact]
    public void IsLocked_true_iff_a_closure_references_the_entry()
    {
        var e = Expense(A, (A, 100), (B, 100));
        var locked = Lookup(Close(e.Id, B, A));

        Assert.True(BalanceCalculator.IsLocked(e, locked));
        Assert.False(BalanceCalculator.IsLocked(e, None));
    }

    // ---- NetWith ---------------------------------------------------------

    [Fact]
    public void NetWith_positive_when_other_owes_me_negative_when_i_owe_other()
    {
        // A pays 300, split equally three ways: B and C each owe A 100.
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        var entries = new[] { e };

        // From A's view, B owes A → +100.
        Assert.Equal(100, BalanceCalculator.NetWith(A, B, entries, None));
        // From B's view, B owes A → -100.
        Assert.Equal(-100, BalanceCalculator.NetWith(B, A, entries, None));
        // A is likewise owed by C.
        Assert.Equal(100, BalanceCalculator.NetWith(A, C, entries, None));
        // B and C have no debt between them.
        Assert.Equal(0, BalanceCalculator.NetWith(B, C, entries, None));
    }

    [Fact]
    public void NetWith_partial_closure_changes_the_net()
    {
        var expense = Expense(A, (A, 100), (B, 100), (C, 100)); // B→A 100, C→A 100
        var iou = Iou(A, B, 30);                                // A owes B 30
        var entries = new Entry[] { expense, iou };

        // Open: A is owed 100 by B, owes 30 to B → +70.
        Assert.Equal(70, BalanceCalculator.NetWith(A, B, entries, None));

        // Close B→A on the expense: only the IOU remains → A owes B 30 → -30.
        var closed = Lookup(Close(expense.Id, B, A));
        Assert.Equal(-30, BalanceCalculator.NetWith(A, B, entries, closed));
    }

    [Fact]
    public void NetWith_aggregates_across_multiple_entries()
    {
        var e1 = Expense(A, (A, 100), (B, 100), (C, 100)); // B→A 100
        var e2 = Expense(A, (A, 50), (B, 50));             // B→A 50
        var e3 = Iou(A, B, 30);                            // A→B 30
        var entries = new[] { e1, e2, e3 };

        // A: +100 +50 -30 = +120 relative to B.
        Assert.Equal(120, BalanceCalculator.NetWith(A, B, entries, None));
    }

    // ---- StatusFor (§2.4) — all five kinds -------------------------------

    [Fact]
    public void StatusFor_settled_when_debts_exist_and_all_closed()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        var closures = Lookup(Close(e.Id, B, A), Close(e.Id, C, A));

        var status = BalanceCalculator.StatusFor(e, A, closures);

        Assert.Equal(ViewerStatusKind.Settled, status.Kind);
        Assert.Equal(0, status.AmountMinor);
    }

    [Fact]
    public void StatusFor_youOwe_sums_viewers_open_debts()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));

        var status = BalanceCalculator.StatusFor(e, B, None);

        Assert.Equal(ViewerStatusKind.YouOwe, status.Kind);
        Assert.Equal(100, status.AmountMinor);
    }

    [Fact]
    public void StatusFor_youAreOwed_sums_debts_owed_to_viewer()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));

        var status = BalanceCalculator.StatusFor(e, A, None);

        Assert.Equal(ViewerStatusKind.YouAreOwed, status.Kind);
        Assert.Equal(200, status.AmountMinor);
    }

    [Fact]
    public void StatusFor_partiallySettled_when_viewer_settled_but_entry_has_open_debts()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));
        // Viewer C's own debt is closed; B→A remains open.
        var closures = Lookup(Close(e.Id, C, A));

        var status = BalanceCalculator.StatusFor(e, C, closures);

        Assert.Equal(ViewerStatusKind.PartiallySettled, status.Kind);
        Assert.Equal(0, status.AmountMinor);
    }

    [Fact]
    public void StatusFor_notYourShare_when_viewer_uninvolved_and_no_closure()
    {
        var e = Expense(A, (A, 100), (B, 100), (C, 100));

        var status = BalanceCalculator.StatusFor(e, D, None);

        Assert.Equal(ViewerStatusKind.NotYourShare, status.Kind);
        Assert.Equal(0, status.AmountMinor);
    }
}
