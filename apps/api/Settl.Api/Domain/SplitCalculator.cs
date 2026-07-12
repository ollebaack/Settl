namespace Settl.Api.Domain;

/// <summary>
/// Pure split math. Freezes integer minor-unit shares with deterministic remainder
/// distribution. No DB / HTTP / time dependencies. Members are always taken in
/// household membership order (see <see cref="MembershipOrder"/>). Every method's shares
/// sum EXACTLY to the amount.
/// </summary>
public static class SplitCalculator
{
    public const decimal PercentTolerance = 0.5m;   // |Σpct − 100| ≤ 0.5
    public const long AmountToleranceMinor = 5;      // |Σvals − A| ≤ 5 öre (±0.05 kr)

    /// <summary>
    /// Equal: integer base to everyone; the first <c>rem</c> members (membership order)
    /// get +1 öre. Example A=10000 N=3 → [3334,3333,3333].
    /// </summary>
    public static IReadOnlyList<(Guid MemberId, long ShareMinor)> Equal(
        IReadOnlyList<Guid> membersInOrder, long amountMinor)
    {
        var n = membersInOrder.Count;
        if (n == 0) return [];

        var baseShare = amountMinor / n;
        var rem = amountMinor - baseShare * n; // 0 ≤ rem < N

        var result = new List<(Guid, long)>(n);
        for (var i = 0; i < n; i++)
            result.Add((membersInOrder[i], baseShare + (i < rem ? 1 : 0)));
        return result;
    }

    /// <summary>
    /// Percent (Hamilton / largest-remainder). Validates |Σpct − 100| ≤ 0.5, then floors
    /// each raw share and hands the leftover öre to the largest fractional parts, tie-broken
    /// by membership order.
    /// </summary>
    public static IReadOnlyList<(Guid MemberId, long ShareMinor)> Percent(
        IReadOnlyList<Guid> membersInOrder, long amountMinor,
        IReadOnlyDictionary<Guid, decimal> percentByMember)
    {
        var n = membersInOrder.Count;
        if (n == 0) return [];

        decimal sumPct = 0;
        foreach (var m in membersInOrder)
            sumPct += percentByMember.TryGetValue(m, out var p) ? p : 0m;

        if (Math.Abs(sumPct - 100m) > PercentTolerance)
            throw new SplitValidationException("Procenten måste bli 100");

        // Normalize by the ACTUAL percent sum (not a literal 100) so shares sum EXACTLY to A
        // even at the tolerance edges (Σpct ∈ [99.5, 100.5]); otherwise leftover can go
        // negative on over-100 sums and the shares would not reconcile to A.
        // raw_m = A * pct_m / Σpct (exact decimal), floor + fractional part.
        var rows = new (Guid Member, long Floor, decimal Frac, int Order)[n];
        long assigned = 0;
        for (var i = 0; i < n; i++)
        {
            var m = membersInOrder[i];
            var pct = percentByMember.TryGetValue(m, out var p) ? p : 0m;
            var raw = amountMinor * pct / sumPct;
            var floor = (long)Math.Floor(raw);
            rows[i] = (m, floor, raw - floor, i);
            assigned += floor;
        }

        var leftover = amountMinor - assigned; // 0 ≤ leftover < N (given valid percentages)

        // Largest fractional part first; tie-break by membership order.
        var byRemainder = rows
            .OrderByDescending(r => r.Frac)
            .ThenBy(r => r.Order)
            .ToArray();

        var bonus = new HashSet<Guid>();
        for (var i = 0; i < leftover && i < n; i++)
            bonus.Add(byRemainder[i].Member);

        return rows
            .Select(r => (r.Member, r.Floor + (bonus.Contains(r.Member) ? 1L : 0L)))
            .ToList();
    }

    /// <summary>
    /// Amount (values are minor units per member). Validates |Σvals − A| ≤ 5 öre, then
    /// reconciles the small difference deterministically (±1 öre at a time in membership
    /// order, never taking a share below 0) so shares sum to exactly A.
    /// </summary>
    public static IReadOnlyList<(Guid MemberId, long ShareMinor)> Amount(
        IReadOnlyList<Guid> membersInOrder, long amountMinor,
        IReadOnlyDictionary<Guid, long> valueByMember)
    {
        var n = membersInOrder.Count;
        if (n == 0) return [];

        var shares = new long[n];
        long sum = 0;
        for (var i = 0; i < n; i++)
        {
            shares[i] = valueByMember.TryGetValue(membersInOrder[i], out var v) ? v : 0L;
            sum += shares[i];
        }

        if (Math.Abs(sum - amountMinor) > AmountToleranceMinor)
            throw new SplitValidationException($"Delningen måste bli {Money.FormatKr(amountMinor)}");

        var diff = amountMinor - sum; // reconcile toward exactly A
        var i2 = 0;
        var guard = 0;
        var maxIterations = (Math.Abs(diff) + 1) * (n + 1);
        while (diff != 0 && guard++ < maxIterations)
        {
            var idx = i2 % n;
            if (diff > 0)
            {
                shares[idx] += 1;
                diff -= 1;
            }
            else if (shares[idx] > 0)
            {
                shares[idx] -= 1;
                diff += 1;
            }
            i2++;
        }

        var result = new List<(Guid, long)>(n);
        for (var i = 0; i < n; i++)
            result.Add((membersInOrder[i], shares[i]));
        return result;
    }
}
