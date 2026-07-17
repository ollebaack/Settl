using Settl.Api.Services;

namespace Settl.Api.Tests;

/// <summary>
/// Unit tests for the Swish pre-fill link builder (swish-settlement-payments spec). Covers the
/// two pieces of money/text logic the API money rule wants pinned down: öre→SEK amount formatting
/// and message sanitization to Swish's allowed charset. The API is authoritative for the URL
/// (ADR-0006); the <c>edit</c> param is intentionally omitted so amount + message stay locked.
/// </summary>
public class SwishLinkTests
{
    [Theory]
    [InlineData(10050, "100.50")]   // spec's worked example
    [InlineData(10000, "100.00")]   // whole kronor still gets two fraction digits
    [InlineData(100, "1.00")]       // one krona
    [InlineData(5, "0.05")]         // five öre
    [InlineData(1, "0.01")]         // one öre
    [InlineData(0, "0.00")]         // zero
    [InlineData(123456789, "1234567.89")] // large, grouping-free, '.' decimal
    public void FormatAmount_renders_ore_as_invariant_sek_decimal(long minor, string expected)
    {
        Assert.Equal(expected, SwishLink.FormatAmount(minor));
    }

    [Theory]
    // Swedish letters, digits, space and the allowed punctuation ! ? ( ) , . - : ; survive.
    [InlineData("Lönnvägen 3", "Settl Lönnvägen 3")]
    [InlineData("Hej! (test), ok.", "Settl Hej! (test), ok.")]
    [InlineData("1-2; på-torget: nu!", "Settl 1-2; på-torget: nu!")]
    public void BuildMessage_prefixes_settl_and_keeps_allowed_characters(string name, string expected)
    {
        Assert.Equal(expected, SwishLink.BuildMessage(name));
    }

    [Theory]
    // Disallowed characters (emoji, & / # = @, and non-Swedish accents like é) are dropped;
    // surrounding allowed chars — including any space that flanked the dropped char — are
    // preserved as-is, no collapsing or trimming.
    [InlineData("A&B", "Settl AB")]
    [InlineData("foo/bar#=", "Settl foobar")]
    [InlineData("Hemmet 🏠", "Settl Hemmet ")]
    [InlineData("Café 100%", "Settl Caf 100")] // é and % dropped (charset is Swedish, not full Latin-1)
    public void BuildMessage_drops_disallowed_characters(string name, string expected)
    {
        Assert.Equal(expected, SwishLink.BuildMessage(name));
    }

    [Fact]
    public void BuildMessage_truncates_to_50_characters()
    {
        var longName = new string('a', 100);
        var msg = SwishLink.BuildMessage(longName);
        Assert.Equal(50, msg.Length);
        Assert.StartsWith("Settl aaaa", msg);
    }

    [Fact]
    public void Build_strips_leading_plus_encodes_params_and_omits_edit()
    {
        var uri = SwishLink.Build("+46701234567", 10050, "Lönnvägen 3");

        Assert.StartsWith("https://app.swish.nu/1/p/sw/?", uri);
        Assert.Contains("sw=46701234567", uri);       // leading '+' stripped
        Assert.Contains("amt=100.50", uri);           // öre → SEK
        Assert.Contains("msg=Settl%20L%C3%B6nnv%C3%A4gen%203", uri); // URL-encoded, sv chars
        Assert.DoesNotContain("edit", uri);           // amount + message stay locked
    }
}
