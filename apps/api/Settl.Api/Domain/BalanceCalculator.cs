namespace Settl.Api.Domain;

/// <summary>A single debt: <see cref="Debtor"/> owes <see cref="Creditor"/> this amount.</summary>
public readonly record struct Debt(Guid Debtor, Guid Creditor, long AmountMinor);

/// <summary>
/// A settlement closure tagged with the timestamp of its parent settlement, for chronological
/// balance-timeline replay (ADR-0023). Direction is stored as recorded.
/// </summary>
public readonly record struct PairClosure(
    Guid EntryId, Guid DebtorMemberId, Guid CreditorMemberId, DateTimeOffset SettledAt);

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
    /// <summary>All debts implied by an entry's frozen shares.</summary>
    public static IReadOnlyList<Debt> Debts(Entry entry)
    {
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

    /// <summary>
    /// The date on which the net between <paramref name="me"/> and <paramref name="x"/> most
    /// recently crossed UP through ±<paramref name="thresholdMinor"/> — |net| transitioning from
    /// below the threshold to at/above it — replayed chronologically over entry
    /// <see cref="Entry.CreatedAt"/> (when a debt appears) and settlement <c>SettledAt</c> (when it
    /// is closed). Returns null if the pair's |net| has never reached the threshold. Backs the
    /// crossing-not-standing balance nudge (ADR-0023) with no stored state.
    ///
    /// Ordering is by when each action was RECORDED, not the accounting <see cref="Entry.Date"/>,
    /// so backdating an entry can neither fake nor hide a fresh crossing. A closure can never take
    /// effect before the entry it closes exists, so its timestamp is clamped to the entry's
    /// CreatedAt — a no-op for real data (a settlement always follows the entry) that also keeps
    /// temporally loose fixtures coherent.
    /// </summary>
    public static DateOnly? MostRecentThresholdCrossing(
        Guid me, Guid x,
        IEnumerable<Entry> householdEntries,
        IEnumerable<PairClosure> closures,
        long thresholdMinor)
    {
        // Closures touching the me↔x pair, keyed by entry (at most one debt per pair per entry).
        var pairClosedAt = closures
            .Where(c => (c.DebtorMemberId == x && c.CreditorMemberId == me)
                     || (c.DebtorMemberId == me && c.CreditorMemberId == x))
            .GroupBy(c => c.EntryId)
            .ToDictionary(g => g.Key, g => g.Min(c => c.SettledAt));

        // Signed deltas to the (me,x) net, each tagged with when it was recorded.
        var events = new List<(DateTimeOffset At, long Delta)>();
        foreach (var entry in householdEntries)
        {
            long signed = 0;
            foreach (var d in Debts(entry))
            {
                if (d.Debtor == x && d.Creditor == me) signed += d.AmountMinor;       // x owes me → net up
                else if (d.Debtor == me && d.Creditor == x) signed -= d.AmountMinor;  // I owe x → net down
            }
            if (signed == 0) continue;

            events.Add((entry.CreatedAt, signed));                    // debt appears
            if (pairClosedAt.TryGetValue(entry.Id, out var closedAt)) // debt later closed
            {
                var effectiveAt = closedAt < entry.CreatedAt ? entry.CreatedAt : closedAt;
                events.Add((effectiveAt, -signed));
            }
        }

        // Walk the timeline; simultaneous events apply together before testing the crossing.
        long net = 0;
        DateOnly? crossedOn = null;
        foreach (var group in events.GroupBy(e => e.At).OrderBy(g => g.Key))
        {
            var before = Math.Abs(net);
            net += group.Sum(e => e.Delta);
            if (before < thresholdMinor && Math.Abs(net) >= thresholdMinor)
                crossedOn = DateOnly.FromDateTime(group.Key.UtcDateTime);
        }
        return crossedOn;
    }

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
