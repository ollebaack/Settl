namespace Settl.Api.Domain;

/// <summary>
/// Bridges a <see cref="SplitMode"/> and its formula inputs to frozen integer shares via
/// <see cref="SplitCalculator"/>, carrying the per-member formula value through. Pure.
/// </summary>
public static class ShareFreezer
{
    public readonly record struct FrozenShare(Guid MemberId, long ShareMinor, decimal? FormulaValue);

    /// <summary>
    /// Freezes shares for a non-Iou entry/template. <paramref name="formulaByMember"/> holds
    /// percent (Percent) or minor units (Amount) per member; ignored for Equal. Throws
    /// <see cref="SplitValidationException"/> on out-of-tolerance percent/amount.
    /// </summary>
    public static IReadOnlyList<FrozenShare> Freeze(
        SplitMode mode,
        IReadOnlyList<Guid> membersInOrder,
        long amountMinor,
        IReadOnlyDictionary<Guid, decimal> formulaByMember)
    {
        switch (mode)
        {
            case SplitMode.Equal:
                return SplitCalculator.Equal(membersInOrder, amountMinor)
                    .Select(s => new FrozenShare(s.MemberId, s.ShareMinor, null))
                    .ToList();

            case SplitMode.Percent:
                return SplitCalculator.Percent(membersInOrder, amountMinor, formulaByMember)
                    .Select(s => new FrozenShare(s.MemberId, s.ShareMinor,
                        formulaByMember.TryGetValue(s.MemberId, out var p) ? p : 0m))
                    .ToList();

            case SplitMode.Amount:
                var valueByMember = membersInOrder.ToDictionary(
                    m => m,
                    m => formulaByMember.TryGetValue(m, out var v) ? (long)v : 0L);
                return SplitCalculator.Amount(membersInOrder, amountMinor, valueByMember)
                    .Select(s => new FrozenShare(s.MemberId, s.ShareMinor,
                        valueByMember.TryGetValue(s.MemberId, out var v) ? v : 0L))
                    .ToList();

            default:
                return [];
        }
    }
}
