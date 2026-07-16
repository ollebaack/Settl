using System.Globalization;
using System.Text;

namespace Settl.Api.Services;

/// <summary>Small helpers shared by registration and invite acceptance.</summary>
public static class AccountHelpers
{
    private static readonly string[] AvatarPalette =
        ["#dfe6cf", "#f0dcc3", "#d9e0ee", "#eed9d9", "#d9eee4", "#e8ddf0"];

    /// <summary>Cycled by email hash — deterministic, no state to track.</summary>
    public static string AvatarColorFor(string email) =>
        AvatarPalette[Math.Abs(email.GetHashCode()) % AvatarPalette.Length];

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try { return new System.Net.Mail.MailAddress(email).Address == email; }
        catch (FormatException) { return false; }
    }

    /// <summary>
    /// Validates/normalizes a user-supplied avatar emoji (ADR-0019). Returns <c>true</c> with
    /// <paramref name="value"/> = the trimmed emoji to store, or <c>null</c> when the input is
    /// null/empty (reset to the letter initial). Returns <c>false</c> when the input is not a
    /// single emoji grapheme — the value is rendered in *other* members' UIs, so the API is
    /// authoritative (ADR-0006), not the client picker.
    ///
    /// A pragmatic guard, not a full Unicode emoji-property test (that would need a shipped data
    /// table): one grapheme cluster (a ZWJ/skin-tone sequence counts as one), length-capped, with
    /// at least one pictographic/symbol code point and no letters or digits.
    /// </summary>
    public static bool TryNormalizeAvatarEmoji(string? input, out string? value, out string? error)
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input)) return true; // null/empty => reset to initial

        var trimmed = input.Trim();

        // Hard length cap first — bounds the work below and blocks oversized payloads.
        if (trimmed.Length is 0 or > 32) { error = "Ogiltig emoji"; return false; }

        // Exactly one grapheme cluster (.NET's segmentation folds ZWJ/skin-tone emoji into one).
        if (new StringInfo(trimmed).LengthInTextElements != 1) { error = "Ogiltig emoji"; return false; }

        var hasSymbol = false;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune)) { error = "Ogiltig emoji"; return false; }
            if (IsEmojiRune(rune)) hasSymbol = true;
        }
        if (!hasSymbol) { error = "Ogiltig emoji"; return false; }

        value = trimmed;
        return true;
    }

    /// <summary>True for pictographic/symbol code points that anchor an emoji. Modifiers
    /// (ZWJ, variation selectors, skin tones, regional indicators) ride along inside the
    /// single grapheme and don't need to individually qualify.</summary>
    private static bool IsEmojiRune(Rune rune)
    {
        var v = rune.Value;
        return v is >= 0x1F000 and <= 0x1FAFF   // supplementary emoji / pictographs
            || v is >= 0x2600 and <= 0x27BF     // misc symbols + dingbats (☕ ✅ …)
            || v is >= 0x2B00 and <= 0x2BFF     // misc symbols & arrows (⭐ = 0x2B50)
            || v is >= 0x2300 and <= 0x23FF     // misc technical (⌚ ⏰ …)
            || v is >= 0x2190 and <= 0x21FF     // arrows
            || v is 0x203C or 0x2049 or 0x00A9 or 0x00AE; // ‼ ⁉ © ®
    }
}
