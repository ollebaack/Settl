namespace Settl.Api.Domain;

/// <summary>Pure Swedish date copy helpers. `today` is always passed in (no time source).</summary>
public static class SwedishDates
{
    private static readonly string[] ShortMonths =
        ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    private static readonly string[] FullMonths =
        ["januari", "februari", "mars", "april", "maj", "juni", "juli", "augusti",
         "september", "oktober", "november", "december"];

    /// <summary>"{d} {mån}" e.g. "11 jul".</summary>
    public static string Short(DateOnly date) => $"{date.Day} {ShortMonths[date.Month - 1]}";

    /// <summary>Full Swedish month name, e.g. "juli".</summary>
    public static string FullMonth(DateOnly date) => FullMonths[date.Month - 1];

    public static int DaysUntil(DateOnly date, DateOnly today) => date.DayNumber - today.DayNumber;

    /// <summary>"idag" / "imorgon" / "om {n} dagar".</summary>
    public static string InDays(DateOnly date, DateOnly today)
    {
        var d = DaysUntil(date, today);
        return d <= 0 ? "idag" : d == 1 ? "imorgon" : $"om {d} dagar";
    }
}
