using System.Text.RegularExpressions;

namespace Settl.Api.Services;

/// <summary>
/// Normalises a typed phone number to E.164 (ADR-0019). The API is authoritative for
/// validation (ADR-0006); the UI's +46 prefix and spacing are cosmetic. A profile phone or
/// SMS-invite number is stored ONLY in E.164 so it is comparable and never ambiguous — but it
/// is still unverified contact data, never a lookup key or auth factor (tech-debt/0010).
/// </summary>
public static partial class PhoneHelpers
{
    private const string DefaultCountryCode = "46"; // Sweden — the app's home market (SEK, sv-SE).

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex E164();

    /// <summary>
    /// Cleans and normalises <paramref name="raw"/> to E.164. Accepts a leading "+", "00"
    /// international prefix, or a Swedish national number (leading "0" → +46). Returns false
    /// for anything that isn't a plausible E.164 number after normalisation.
    /// </summary>
    public static bool TryNormalize(string? raw, out string e164)
    {
        e164 = "";
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // Keep digits and a single leading plus; drop spaces, dashes, parentheses, etc.
        var trimmed = raw.Trim();
        var hasPlus = trimmed.StartsWith('+');
        var digits = DigitsOnly().Replace(trimmed, "");
        if (digits.Length == 0) return false;

        string candidate;
        if (hasPlus)
        {
            candidate = "+" + digits;
        }
        else if (digits.StartsWith("00"))
        {
            candidate = "+" + digits[2..];
        }
        else if (digits.StartsWith('0'))
        {
            // National Swedish format: strip the trunk 0, prepend the country code.
            candidate = "+" + DefaultCountryCode + digits[1..];
        }
        else
        {
            // Already-country-coded without a plus, or a bare subscriber number — assume the
            // default region so a user typing "701234567" behind the UI's +46 chip works.
            candidate = digits.StartsWith(DefaultCountryCode)
                ? "+" + digits
                : "+" + DefaultCountryCode + digits;
        }

        if (!E164().IsMatch(candidate)) return false;
        e164 = candidate;
        return true;
    }

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex DigitsOnly();
}
