using System.Globalization;
using System.Text.RegularExpressions;

namespace Settl.Api.Services;

/// <summary>
/// Builds a Swish pre-fill deep link (swish-settlement-payments spec) from an API-authoritative
/// amount + recipient. This is the FREE path: a self-generated link per the public Swish QR
/// specification, not the paid Commerce API — no token, certificate or merchant agreement. The
/// <c>edit</c> parameter is omitted so Swish locks both the amount and the message; the ledger
/// figure is authoritative (ADR-0006). Money stays integer minor units until the very last step.
/// </summary>
public static partial class SwishLink
{
    private const string BaseUrl = "https://app.swish.nu/1/p/sw/";

    /// <summary>Swish's allowed message charset (QR code spec v1.7.2): Swedish letters
    /// (a–z, A–Z, å ä ö Å Ä Ö), digits, the punctuation <c>! ? ( ) , . - : ;</c> and space.
    /// Anything else (emoji, other scripts, control chars) is dropped. Interpreted as the
    /// Swedish alphanumeric set the spec intends — a literal <c>a-öA-Ö</c> code-point range
    /// would wrongly admit <c>[ \ ] ^ _ `</c> and similar between Z and a.</summary>
    [GeneratedRegex(@"[^A-Za-z0-9åäöÅÄÖ !?(),.\-:;]")]
    private static partial Regex Disallowed();

    /// <summary>öre → SEK decimal for the <c>amt</c> param: <c>10050</c> → <c>"100.50"</c>.
    /// Invariant '.' decimal separator, always two fraction digits. Callers pass a non-negative
    /// amount (the absolute debt).</summary>
    public static string FormatAmount(long amountMinor) =>
        (amountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary><c>"Settl {household}"</c> sanitized to Swish's allowed charset, disallowed
    /// characters dropped, then truncated to the 50-char message limit.</summary>
    public static string BuildMessage(string householdName)
    {
        var cleaned = Disallowed().Replace($"Settl {householdName}", "");
        return cleaned.Length > 50 ? cleaned[..50] : cleaned;
    }

    /// <summary>The full pre-fill URL. <paramref name="payeeE164"/> is the creditor's E.164 Swish
    /// number; its leading <c>+</c> is stripped for the <c>sw</c> param. All params are
    /// URL-encoded; <c>edit</c> is intentionally omitted so amount and message stay locked.</summary>
    public static string Build(string payeeE164, long amountMinor, string householdName)
    {
        var sw = payeeE164.TrimStart('+');
        var amt = FormatAmount(amountMinor);
        var msg = BuildMessage(householdName);
        return $"{BaseUrl}?sw={Uri.EscapeDataString(sw)}" +
               $"&amt={Uri.EscapeDataString(amt)}" +
               $"&msg={Uri.EscapeDataString(msg)}";
    }
}
