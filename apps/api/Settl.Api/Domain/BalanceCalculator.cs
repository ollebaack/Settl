namespace Settl.Api.Domain;

/// <summary>A single debt: <see cref="Debtor"/> owes <see cref="Creditor"/> this amount.</summary>
public readonly record struct Debt(Guid Debtor, Guid Creditor, long AmountMinor);

public enum ViewerStatusKind
{
    Settled,
    YouOwe,
    YouAreOwed,
    PartiallySettled,
    NotYourShare
}

public readonly record struct ViewerStatus(ViewerStatusKind Kind, long AmountMinor);

/// <summary>
/// Direction-normalized view over settlement closures. A debt is closed iff a closure exists
/// for its entry and unordered member pair, regardless of stored direction.
/// </summary>
public sealed class ClosureLookup
{
    private readonly HashSet<(Guid Entry, Guid A, Guid B)> _pairs = [];
    private readonly HashSet<Guid> _entries = [];

    public ClosureLookup(IEnumerable<SettlementClosure> closures)
    {
        foreach (var c in closures)
        {
            _pairs.Add((c.EntryId, Min(c.DebtorMemberId, c.CreditorMemberId), Max(c.DebtorMemberId, c.CreditorMemberId)));
            _entries.Add(c.EntryId);
        }
    }

    public bool IsClosed(Guid entryId, Guid a, Guid b) =>
        _pairs.Contains((entryId, Min(a, b), Max(a, b)));

    /// <summary>True if any closure references this entry (→ entry is locked).</summary>
    public bool AnyForEntry(Guid entryId) => _entries.Contains(entryId);

    private static Guid Min(Guid a, Guid b) => a.CompareTo(b) <= 0 ? a : b;
    private static Guid Max(Guid a, Guid b) => a.CompareTo(b) <= 0 ? b : a;
}

/// <summary>
/// Pure derivation of debts, open/closed state, net balances and viewer-relative status.
/// No DB / HTTP deps — operates on loaded entities and a <see cref="ClosureLookup"/>.
/// </summary>
public static class BalanceCalculator
{
    /// <summary>All debts implied by an entry's frozen shares (or its Iou direction).</summary>
    public static IReadOnlyList<Debt> Debts(Entry entry)
    {
        if (entry.Type == EntryType.Iou)
        {
            if (entry.FromMemberId is null || entry.ToMemberId is null) return [];
            return [new Debt(entry.FromMemberId.Value, entry.ToMemberId.Value, entry.AmountMinor)];
        }

        if (entry.PaidByMemberId is null) return [];
        var paidBy = entry.PaidByMemberId.Value;

        return entry.Shares
            .Where(s => s.ShareMinor > 0 && s.MemberId != paidBy)
            .Select(s => new Debt(s.MemberId, paidBy, s.ShareMinor))
            .ToList();
    }

    public static IReadOnlyList<Debt> OpenDebts(Entry entry, ClosureLookup closures) =>
        Debts(entry).Where(d => !closures.IsClosed(entry.Id, d.Debtor, d.Creditor)).ToList();

    /// <summary>Derived: Debts non-empty AND all closed.</summary>
    public static bool IsSettled(Entry entry, ClosureLookup closures)
    {
        var debts = Debts(entry);
        return debts.Count > 0 && OpenDebts(entry, closures).Count == 0;
    }

    /// <summary>Derived: at least one closure references the entry.</summary>
    public static bool IsLocked(Entry entry, ClosureLookup closures) => closures.AnyForEntry(entry.Id);

    /// <summary>
    /// Net between viewer <paramref name="me"/> and member <paramref name="x"/> across a
    /// household's entries. &gt;0 → X owes me; &lt;0 → I owe X.
    /// </summary>
    public static long NetWith(Guid me, Guid x, IEnumerable<Entry> householdEntries, ClosureLookup closures)
    {
        long net = 0;
        foreach (var entry in householdEntries)
        {
            foreach (var d in OpenDebts(entry, closures))
            {
                if (d.Debtor == x && d.Creditor == me) net += d.AmountMinor;
                else if (d.Debtor == me && d.Creditor == x) net -= d.AmountMinor;
            }
        }
        return net;
    }

    /// <summary>
    /// Total of every open debt across a household's entries — the household-wide "still owed"
    /// figure shown on the archive warning (ADR-0016). Not viewer-relative.
    /// </summary>
    public static long HouseholdOpenTotalMinor(IEnumerable<Entry> householdEntries, ClosureLookup closures) =>
        householdEntries.Sum(e => OpenDebts(e, closures).Sum(d => d.AmountMinor));

    /// <summary>Viewer-relative status for a single entry (§2.4).</summary>
    public static ViewerStatus StatusFor(Entry entry, Guid me, ClosureLookup closures)
    {
        var debts = Debts(entry);
        var open = OpenDebts(entry, closures);

        if (debts.Count > 0 && open.Count == 0)
            return new ViewerStatus(ViewerStatusKind.Settled, 0);

        long owe = 0, owed = 0;
        foreach (var d in open)
        {
            if (d.Debtor == me) owe += d.AmountMinor;
            if (d.Creditor == me) owed += d.AmountMinor;
        }

        if (owe > 0) return new ViewerStatus(ViewerStatusKind.YouOwe, owe);
        if (owed > 0) return new ViewerStatus(ViewerStatusKind.YouAreOwed, owed);
        if (closures.AnyForEntry(entry.Id)) return new ViewerStatus(ViewerStatusKind.PartiallySettled, 0);
        return new ViewerStatus(ViewerStatusKind.NotYourShare, 0);
    }
}
