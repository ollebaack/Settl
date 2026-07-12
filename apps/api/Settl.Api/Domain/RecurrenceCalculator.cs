namespace Settl.Api.Domain;

/// <summary>
/// Pure recurrence logic. No DB / time source — `today` is always a parameter so the
/// hosted service and unit tests share the same code.
/// </summary>
public static class RecurrenceCalculator
{
    public static DateOnly Advance(DateOnly date, Cadence cadence) => cadence switch
    {
        Cadence.Monthly => date.AddMonths(1),
        Cadence.Biweekly => date.AddDays(14),
        Cadence.Weekly => date.AddDays(7),
        _ => throw new ArgumentOutOfRangeException(nameof(cadence))
    };

    /// <summary>Nominal cycle length in days, for progress only.</summary>
    public static int CycleLengthDays(Cadence cadence) => cadence switch
    {
        Cadence.Monthly => 30,
        Cadence.Biweekly => 14,
        Cadence.Weekly => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(cadence))
    };

    /// <summary>Elapsed share of the current cycle, clamped to [0.04, 1.0]; 0 when inactive.</summary>
    public static double CycleProgress(DateOnly nextPostDate, Cadence cadence, DateOnly today, bool active)
    {
        if (!active) return 0d;
        var daysLeft = nextPostDate.DayNumber - today.DayNumber;
        var raw = 1d - (double)daysLeft / CycleLengthDays(cadence);
        return Math.Clamp(raw, 0.04d, 1.0d);
    }

    /// <summary>Monthly-normalized amount: Monthly ×1, Biweekly ×2, Weekly ×4. Used for BOTH recTotal and recShare.</summary>
    public static long MonthlyNormalizedMinor(long amountMinor, Cadence cadence) => cadence switch
    {
        Cadence.Monthly => amountMinor,
        Cadence.Biweekly => amountMinor * 2,
        Cadence.Weekly => amountMinor * 4,
        _ => throw new ArgumentOutOfRangeException(nameof(cadence))
    };

    /// <summary>
    /// Advances a post date forward by whole cycles until it is on or after <paramref name="today"/>.
    /// Used on RESUME so a paused-through gap is skipped (scheduling the next period) rather than
    /// back-posted in a burst. Downtime catch-up for always-active templates is unaffected.
    /// </summary>
    public static DateOnly FastForwardToOnOrAfter(DateOnly nextPostDate, Cadence cadence, DateOnly today)
    {
        var cursor = nextPostDate;
        var guard = 0;
        while (cursor.DayNumber < today.DayNumber && guard++ < 100_000)
            cursor = Advance(cursor, cadence);
        return cursor;
    }

    /// <summary>
    /// Yields every post date due up to and including <paramref name="today"/> for an active
    /// template, starting at its NextPostDate. Deterministic and terminating.
    /// </summary>
    public static IEnumerable<DateOnly> DuePosts(bool active, DateOnly nextPostDate, Cadence cadence, DateOnly today)
    {
        if (!active) yield break;
        var cursor = nextPostDate;
        while (cursor.DayNumber <= today.DayNumber)
        {
            yield return cursor;
            cursor = Advance(cursor, cadence);
        }
    }
}
