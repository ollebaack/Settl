namespace Settl.Api.Domain;

/// <summary>
/// Pure scheduling policy for the daily nudge digest (reminder-delivery spec). Timestamps stay
/// UTC everywhere (project rule); this only decides WHEN in the day a digest may go out, and it
/// does so in Sweden's local zone so an "08:00" send never lands at 02:00 local across DST. No
/// clock of its own — the caller passes <c>utcNow</c>.
/// </summary>
public static class DigestSchedule
{
    /// <summary>Fixed send hour (local), 08:00 Europe/Stockholm — a sane morning default for v1
    /// (the spec leaves configurability out of scope).</summary>
    public const int SendHourLocal = 8;

    /// <summary>Europe/Stockholm, resolved by IANA id with a Windows-id fallback so the same code
    /// works under Linux (containers, ADR-0014) and Windows dev. DST is handled by TimeZoneInfo.</summary>
    public static readonly TimeZoneInfo StockholmZone = ResolveStockholm();

    /// <summary>The Stockholm-local calendar date at <paramref name="utcNow"/> — the granularity of
    /// "one digest per day".</summary>
    public static DateOnly LocalDate(DateTimeOffset utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, StockholmZone).DateTime);

    /// <summary>True once the local time is at/after the send hour on <paramref name="utcNow"/>'s
    /// local day.</summary>
    public static bool IsPastSendHour(DateTimeOffset utcNow) =>
        TimeZoneInfo.ConvertTime(utcNow, StockholmZone).Hour >= SendHourLocal;

    /// <summary>
    /// Whether the daily digest pass should run now: past the local send hour, and not already run
    /// for today's local date. <paramref name="lastRunLocalDate"/> is null before the first run.
    /// </summary>
    public static bool ShouldRun(DateTimeOffset utcNow, DateOnly? lastRunLocalDate) =>
        IsPastSendHour(utcNow) && lastRunLocalDate != LocalDate(utcNow);

    /// <summary>The UTC instant of local midnight starting <paramref name="utcNow"/>'s local day.
    /// The lower bound for "was this member already emailed today (local)?" — the per-recipient
    /// once-a-day guard that survives a process restart.</summary>
    public static DateTimeOffset LocalDayStartUtc(DateTimeOffset utcNow)
    {
        var d = LocalDate(utcNow);
        var localMidnight = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, StockholmZone), TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveStockholm()
    {
        foreach (var id in new[] { "Europe/Stockholm", "W. Europe Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Last resort: a fixed +01:00 keeps the digest in the morning rather than crashing the
        // background service if neither tz database entry is present.
        return TimeZoneInfo.CreateCustomTimeZone("Settl-CET", TimeSpan.FromHours(1), "CET", "CET");
    }
}
