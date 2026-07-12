using System.Globalization;

namespace Settl.Api.Domain;

/// <summary>
/// sv-SE money formatting for user-facing copy (nudges, validation detail).
/// Money is integer minor units (öre); major = minor / 100.
/// </summary>
public static class Money
{
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>
    /// Absolute value formatted sv-SE with a non-breaking space (U+00A0) before "kr",
    /// grouping separator and comma decimal, 0..2 fraction digits. E.g. 240000 → "2 400 kr".
    /// </summary>
    public static string FormatKr(long minor)
    {
        var major = Math.Abs(minor) / 100m;
        return major.ToString("#,0.##", Sv) + " kr";
    }
}
