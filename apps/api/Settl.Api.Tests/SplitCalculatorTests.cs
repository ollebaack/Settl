using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Tests;

/// <summary>
/// Exhaustive pure unit tests for <see cref="SplitCalculator"/> and <see cref="ShareFreezer"/>.
/// No DB/HTTP/time deps. Members are a fixed ordered list (seed member ids) so the deterministic
/// remainder/tie-break behaviour is asserted against real membership order. Every expected array
/// is the exact computed value; every mode's shares must sum EXACTLY to the amount.
/// </summary>
public class SplitCalculatorTests
{
    // Fixed ordered membership list. Order IS the tie-break/remainder order.
    private static readonly Guid M1 = SeedIds.Du;
    private static readonly Guid M2 = SeedIds.Sam;
    private static readonly Guid M3 = SeedIds.Priya;
    private static readonly Guid M4 = SeedIds.Mamma;
    private static readonly Guid M5 = SeedIds.Pappa;

    private static readonly IReadOnlyList<Guid> Members3 = new[] { M1, M2, M3 };
    private static readonly IReadOnlyList<Guid> Members2 = new[] { M1, M2 };
    private static readonly IReadOnlyList<Guid> Members1 = new[] { M1 };
    private static readonly IReadOnlyList<Guid> Members4 = new[] { M1, M2, M3, M4 };

    /// <summary>Pull the shares out in membership order for exact array assertions.</summary>
    private static long[] Shares(IReadOnlyList<(Guid MemberId, long ShareMinor)> result) =>
        result.Select(r => r.ShareMinor).ToArray();

    private static void AssertMembersInOrder(
        IReadOnlyList<Guid> expected, IReadOnlyList<(Guid MemberId, long ShareMinor)> result)
    {
        Assert.Equal(expected.Count, result.Count);
        for (var i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], result[i].MemberId);
    }

    // ---------------------------------------------------------------- Equal

    [Fact]
    public void Equal_canonical_example_10000_over_3_gives_3334_3333_3333()
    {
        var result = SplitCalculator.Equal(Members3, 10000);
        Assert.Equal(new long[] { 3334, 3333, 3333 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
        AssertMembersInOrder(Members3, result); // remainder went to the FIRST member
    }

    [Fact]
    public void Equal_remainder_zero_distributes_evenly()
    {
        // 90000 / 3 = 30000 exactly, rem 0.
        var result = SplitCalculator.Equal(Members3, 90000);
        Assert.Equal(new long[] { 30000, 30000, 30000 }, Shares(result));
        Assert.Equal(90000, Shares(result).Sum());
    }

    [Fact]
    public void Equal_not_divisible_small_amount_100_over_3()
    {
        // base 33, rem 1 -> first member +1.
        var result = SplitCalculator.Equal(Members3, 100);
        Assert.Equal(new long[] { 34, 33, 33 }, Shares(result));
        Assert.Equal(100, Shares(result).Sum());
    }

    [Fact]
    public void Equal_N1_gives_whole_amount()
    {
        var result = SplitCalculator.Equal(Members1, 12345);
        Assert.Equal(new long[] { 12345 }, Shares(result));
    }

    [Fact]
    public void Equal_N2_odd_amount_first_gets_remainder()
    {
        // 10001 / 2 = 5000, rem 1 -> first member +1.
        var result = SplitCalculator.Equal(Members2, 10001);
        Assert.Equal(new long[] { 5001, 5000 }, Shares(result));
        Assert.Equal(10001, Shares(result).Sum());
    }

    [Fact]
    public void Equal_rem_two_gives_bonus_to_first_two_members()
    {
        // 11 / 3 = 3, rem 2 -> first two members +1 -> [4,4,3].
        var result = SplitCalculator.Equal(Members3, 11);
        Assert.Equal(new long[] { 4, 4, 3 }, Shares(result));
        Assert.Equal(11, Shares(result).Sum());
    }

    [Fact]
    public void Equal_large_amount_sums_exactly_and_remainder_is_deterministic()
    {
        // 1_000_000_007 / 3 = 333_333_335, rem 2 -> first two +1.
        const long a = 1_000_000_007L;
        var result = SplitCalculator.Equal(Members3, a);
        Assert.Equal(new long[] { 333_333_336, 333_333_336, 333_333_335 }, Shares(result));
        Assert.Equal(a, Shares(result).Sum());
    }

    [Fact]
    public void Equal_empty_members_returns_empty()
    {
        Assert.Empty(SplitCalculator.Equal(Array.Empty<Guid>(), 10000));
    }

    // ---------------------------------------------------------------- Percent

    private static IReadOnlyDictionary<Guid, decimal> Pct(params (Guid, decimal)[] pairs) =>
        pairs.ToDictionary(p => p.Item1, p => p.Item2);

    [Fact]
    public void Percent_40_40_20_exact_integers()
    {
        var result = SplitCalculator.Percent(Members3, 10000,
            Pct((M1, 40m), (M2, 40m), (M3, 20m)));
        Assert.Equal(new long[] { 4000, 4000, 2000 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_33_33__33_33__33_34_sums_to_amount()
    {
        var result = SplitCalculator.Percent(Members3, 10000,
            Pct((M1, 33.33m), (M2, 33.33m), (M3, 33.34m)));
        Assert.Equal(new long[] { 3333, 3333, 3334 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_integer_50_50_on_odd_amount_ties_break_by_membership_order()
    {
        // 10001 * 50/100 = 5000.5 each; leftover 1; equal fracs -> first member wins.
        var result = SplitCalculator.Percent(Members2, 10001,
            Pct((M1, 50m), (M2, 50m)));
        Assert.Equal(new long[] { 5001, 5000 }, Shares(result));
        Assert.Equal(10001, Shares(result).Sum());
    }

    [Fact]
    public void Percent_all_equal_fracs_distribute_leftover_to_first_members_in_order()
    {
        // A=10, 4 members @25%: raw 2.5 each, floor 2, leftover 2; all fracs equal ->
        // first two members get +1 -> [3,3,2,2].
        var result = SplitCalculator.Percent(Members4, 10,
            Pct((M1, 25m), (M2, 25m), (M3, 25m), (M4, 25m)));
        Assert.Equal(new long[] { 3, 3, 2, 2 }, Shares(result));
        Assert.Equal(10, Shares(result).Sum());
    }

    [Fact]
    public void Percent_largest_remainder_orders_by_fractional_part()
    {
        // A=1000, pct 16.67/16.67/66.66 -> raw 166.7,166.7,666.6; floors 166,166,666 (=998);
        // leftover 2; fracs .7,.7,.6 -> top two are the two .7 members (tie-break by order).
        var result = SplitCalculator.Percent(Members3, 1000,
            Pct((M1, 16.67m), (M2, 16.67m), (M3, 66.66m)));
        Assert.Equal(new long[] { 167, 167, 666 }, Shares(result));
        Assert.Equal(1000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_throws_when_sum_below_tolerance()
    {
        var ex = Assert.Throws<SplitValidationException>(() =>
            SplitCalculator.Percent(Members3, 10000, Pct((M1, 40m), (M2, 40m), (M3, 19m)))); // sum 99, |99-100|=1
        Assert.Equal("Procenten måste bli 100", ex.Message);
    }

    [Fact]
    public void Percent_throws_when_sum_above_tolerance()
    {
        var ex = Assert.Throws<SplitValidationException>(() =>
            SplitCalculator.Percent(Members2, 10000, Pct((M1, 50.3m), (M2, 50.3m)))); // sum 100.6
        Assert.Equal("Procenten måste bli 100", ex.Message);
    }

    [Fact]
    public void Percent_accepts_sum_995_at_tolerance_edge_and_sums_exactly_to_amount()
    {
        // sum 99.5 (|99.5-100|=0.5, accepted). Normalized by actual sum so shares total A exactly.
        // 49.75/49.75 over 99.5 -> 10000*49.75/99.5 = 5000 each.
        var result = SplitCalculator.Percent(Members2, 10000,
            Pct((M1, 49.75m), (M2, 49.75m)));
        Assert.Equal(new long[] { 5000, 5000 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_accepts_sum_1005_at_tolerance_edge_and_sums_exactly_to_amount()
    {
        // sum 100.5 accepted. Naive /100 would over-assign (5025 each = 10050 > A) with negative
        // leftover; normalizing by the ACTUAL sum keeps shares reconciled to A.
        var result = SplitCalculator.Percent(Members2, 10000,
            Pct((M1, 50.25m), (M2, 50.25m)));
        Assert.Equal(new long[] { 5000, 5000 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_within_tolerance_with_leftover_normalizes_and_reconciles_exactly()
    {
        // sum 99.5, uneven: 33/33/33.5. Normalized raws produce leftover 2 handed to the
        // largest fractional parts (m3 then m1), tie-break by order for the two equal .58 fracs.
        var result = SplitCalculator.Percent(Members3, 10000,
            Pct((M1, 33m), (M2, 33m), (M3, 33.5m)));
        Assert.Equal(new long[] { 3317, 3316, 3367 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Percent_empty_members_returns_empty()
    {
        Assert.Empty(SplitCalculator.Percent(Array.Empty<Guid>(), 10000, Pct()));
    }

    // ---------------------------------------------------------------- Amount

    private static IReadOnlyDictionary<Guid, long> Val(params (Guid, long)[] pairs) =>
        pairs.ToDictionary(p => p.Item1, p => p.Item2);

    [Fact]
    public void Amount_exact_sum_returned_as_is()
    {
        var result = SplitCalculator.Amount(Members3, 10000,
            Val((M1, 5000), (M2, 3000), (M3, 2000)));
        Assert.Equal(new long[] { 5000, 3000, 2000 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Amount_positive_diff_of_3_ore_reconciles_forward_in_order()
    {
        // sum 9997, diff +3 -> +1 to first three members.
        var result = SplitCalculator.Amount(Members3, 10000,
            Val((M1, 3333), (M2, 3333), (M3, 3331)));
        Assert.Equal(new long[] { 3334, 3334, 3332 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Amount_negative_diff_of_3_ore_reconciles_downward_in_order()
    {
        // sum 10003, diff -3 -> -1 from first three members.
        var result = SplitCalculator.Amount(Members3, 10000,
            Val((M1, 3335), (M2, 3334), (M3, 3334)));
        Assert.Equal(new long[] { 3334, 3333, 3333 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Amount_positive_diff_at_tolerance_edge_5_reconciles_exactly()
    {
        // sum 9995, diff +5 (edge) -> wraps: +1 idx0,1,2 then idx0,1 again.
        var result = SplitCalculator.Amount(Members3, 10000,
            Val((M1, 3332), (M2, 3332), (M3, 3331)));
        Assert.Equal(new long[] { 3334, 3334, 3332 }, Shares(result));
        Assert.Equal(10000, Shares(result).Sum());
    }

    [Fact]
    public void Amount_never_drives_a_share_below_zero()
    {
        // A=10, vals [0,7,6] sum 13, diff -3. idx0 (0) is skipped every pass; the -3 is taken
        // from members with a positive share -> [0,5,5]. No share goes negative.
        var result = SplitCalculator.Amount(Members3, 10,
            Val((M1, 0), (M2, 7), (M3, 6)));
        Assert.Equal(new long[] { 0, 5, 5 }, Shares(result));
        Assert.Equal(10, Shares(result).Sum());
        Assert.All(Shares(result), s => Assert.True(s >= 0));
    }

    [Fact]
    public void Amount_throws_when_diff_exceeds_tolerance_with_kr_formatted_message()
    {
        // sum 9994, |diff|=6 > 5 -> throws. Message uses sv-SE kr formatting of A.
        var ex = Assert.Throws<SplitValidationException>(() =>
            SplitCalculator.Amount(Members3, 10000,
                Val((M1, 3333), (M2, 3333), (M3, 3328))));
        // FormatKr uses a non-breaking space (U+00A0) before "kr" -> assert via the formatter.
        Assert.Equal("Delningen måste bli " + Money.FormatKr(10000), ex.Message);
    }

    [Fact]
    public void Amount_N1_reconciles_single_member_to_amount()
    {
        // vals [4998], A=5000, diff +2 -> [5000].
        var result = SplitCalculator.Amount(Members1, 5000, Val((M1, 4998)));
        Assert.Equal(new long[] { 5000 }, Shares(result));
    }

    [Fact]
    public void Amount_empty_members_returns_empty()
    {
        Assert.Empty(SplitCalculator.Amount(Array.Empty<Guid>(), 10000, Val()));
    }

    // ---------------------------------------------------------------- ShareFreezer

    [Fact]
    public void Freeze_none_mode_returns_empty_no_shares_for_iou()
    {
        // Iou path: SplitMode.None produces no EntryShare rows.
        var result = ShareFreezer.Freeze(SplitMode.None, Members3, 10000,
            new Dictionary<Guid, decimal>());
        Assert.Empty(result);
    }

    [Fact]
    public void Freeze_equal_sets_null_formula_value_and_matches_calculator()
    {
        var result = ShareFreezer.Freeze(SplitMode.Equal, Members3, 10000,
            new Dictionary<Guid, decimal>());
        Assert.Equal(new long[] { 3334, 3333, 3333 }, result.Select(r => r.ShareMinor).ToArray());
        Assert.All(result, s => Assert.Null(s.FormulaValue));
        Assert.Equal(Members3, result.Select(r => r.MemberId).ToArray());
    }

    [Fact]
    public void Freeze_percent_carries_percent_as_formula_value()
    {
        var formula = new Dictionary<Guid, decimal> { [M1] = 40m, [M2] = 40m, [M3] = 20m };
        var result = ShareFreezer.Freeze(SplitMode.Percent, Members3, 10000, formula);
        Assert.Equal(new long[] { 4000, 4000, 2000 }, result.Select(r => r.ShareMinor).ToArray());
        Assert.Equal(new decimal?[] { 40m, 40m, 20m }, result.Select(r => r.FormulaValue).ToArray());
    }

    [Fact]
    public void Freeze_amount_casts_minor_units_and_carries_them_as_formula_value()
    {
        var formula = new Dictionary<Guid, decimal> { [M1] = 5000m, [M2] = 3000m, [M3] = 2000m };
        var result = ShareFreezer.Freeze(SplitMode.Amount, Members3, 10000, formula);
        Assert.Equal(new long[] { 5000, 3000, 2000 }, result.Select(r => r.ShareMinor).ToArray());
        Assert.Equal(new decimal?[] { 5000m, 3000m, 2000m }, result.Select(r => r.FormulaValue).ToArray());
    }

    [Fact]
    public void Freeze_percent_propagates_validation_exception()
    {
        var formula = new Dictionary<Guid, decimal> { [M1] = 40m, [M2] = 40m, [M3] = 19m };
        Assert.Throws<SplitValidationException>(() =>
            ShareFreezer.Freeze(SplitMode.Percent, Members3, 10000, formula));
    }
}
