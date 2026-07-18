using Settl.Api.Services;

namespace Settl.Api.Tests;

/// <summary>Phone normalisation to E.164 (contacts-phone-sms spec). The API is authoritative for validation
/// (ADR-0006), so this covers the shapes the UI's +46 chip can produce plus junk input.</summary>
public class PhoneHelpersTests
{
    [Theory]
    [InlineData("+46701234567", "+46701234567")]     // already E.164
    [InlineData("070-123 45 67", "+46701234567")]     // Swedish national, spaced/dashed
    [InlineData("0701234567", "+46701234567")]        // Swedish national, trunk 0
    [InlineData("701234567", "+46701234567")]         // bare subscriber (behind the +46 chip)
    [InlineData("0046701234567", "+46701234567")]     // 00 international prefix
    [InlineData("+1 (202) 555-0143", "+12025550143")] // non-Swedish, kept as typed
    public void TryNormalize_accepts_and_normalises(string input, string expected)
    {
        Assert.True(PhoneHelpers.TryNormalize(input, out var e164));
        Assert.Equal(expected, e164);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12")]        // too short for E.164
    [InlineData("+0123456")]  // E.164 must not start with 0 after the plus
    public void TryNormalize_rejects_junk(string? input)
    {
        Assert.False(PhoneHelpers.TryNormalize(input, out var e164));
        Assert.Equal("", e164);
    }
}
