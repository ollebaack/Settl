using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Services;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for the Swish settlement feature
/// (swish-settlement-payments spec): the profile write that stores a member's Swish number
/// (PUT /me) and the <c>swishPay</c> pre-fill link surfaced on the settle-preview read model.
/// The link is a convenience launcher for the debtor only, SEK-only, and needs the creditor to
/// have opted in with a Swish number — no confirmation, settling stays the manual action.
/// </summary>
public class SwishSettlementTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    // ---------- Profile write (PUT /me) ----------

    [Fact]
    public async Task Put_me_stores_swish_number_normalised_to_e164()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        // Typed behind the UI's +46 chip as a Swedish national number.
        var res = await client.PutAsJsonAsync("/me",
            new UpdateMeRequest("Du", null, SwishNumber: "070-123 45 67"), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updated = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal("+46701234567", updated!.SwishNumber);

        // Persisted — a fresh read reflects it.
        var me = await client.GetFromJsonAsync<MeDto>("/me", Web);
        Assert.Equal("+46701234567", me!.SwishNumber);
    }

    [Fact]
    public async Task Put_me_empty_swish_number_clears_it()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        await client.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, SwishNumber: "0701234567"), Web);
        var res = await client.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, SwishNumber: "   "), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var updated = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Null(updated!.SwishNumber);
    }

    [Fact]
    public async Task Put_me_rejects_unparseable_swish_number()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        var res = await client.PutAsJsonAsync("/me",
            new UpdateMeRequest("Du", null, SwishNumber: "not-a-number"), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Ogiltigt Swish-nummer", await DetailAsync(res));
    }

    [Fact]
    public async Task Put_me_swish_number_is_independent_of_profile_phone()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        // Set a profile phone (PATCH /me) and a DIFFERENT Swish number (PUT /me); neither derives
        // from the other.
        await client.PatchAsJsonAsync("/me", new UpdateProfileRequest("0701111111"), Web);
        await client.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, SwishNumber: "0702222222"), Web);

        var me = await client.GetFromJsonAsync<MeDto>("/me", Web);
        Assert.Equal("+46701111111", me!.Phone);
        Assert.Equal("+46702222222", me.SwishNumber);
    }

    // ---------- swishPay on settle-preview ----------

    /// <summary>Two-member SEK household where <c>debtor</c> owes <c>creditor</c> 5000 öre
    /// (creditor paid a 10000 equal-split expense). Returns (factory, householdId, debtorId,
    /// creditorId).</summary>
    private static async Task<(SettlApiFactory Factory, Guid HouseholdId, Guid Debtor, Guid Creditor)>
        DebtScenarioAsync(string currency = "SEK", string? creditorSwish = "+46701234567")
    {
        var scenario = new TestScenario("Testhushåll", currency);
        var debtor = scenario.AddMember("Anna");
        var creditor = scenario.AddMember("Bertil");
        // Creditor pays; equal split across the two → debtor owes creditor half.
        scenario.AddEqualExpense("Middag", 10_000, creditor);

        var factory = new SettlApiFactory();
        await factory.SeedAsync(scenario);

        if (creditorSwish is not null)
            await factory.WithDb(async db =>
            {
                var m = await db.Members.FindAsync(creditor);
                m!.SwishNumber = creditorSwish;
                await db.SaveChangesAsync();
            });

        return (factory, scenario.HouseholdId, debtor, creditor);
    }

    [Fact]
    public async Task SettlePreview_debtor_gets_swishPay_when_creditor_has_number_and_sek()
    {
        var (factory, hid, debtor, creditor) = await DebtScenarioAsync();
        using var _ = factory;
        var client = factory.ClientAs(debtor);

        var preview = await client.GetFromJsonAsync<SettlePreviewDto>(
            $"/households/{hid}/settle-preview?person={creditor}", Web);

        Assert.NotNull(preview);
        Assert.Equal(-5000, preview!.NetMinor); // debtor owes 5000
        Assert.Equal("youOwe", preview.NetLabel);
        Assert.NotNull(preview.SwishPay);
        Assert.Equal(5000, preview.SwishPay!.AmountMinor);
        // The endpoint wires the creditor's number, absolute amount and household name into the
        // (independently unit-tested) builder.
        Assert.Equal(SwishLink.Build("+46701234567", 5000, "Testhushåll"), preview.SwishPay.Uri);
    }

    [Fact]
    public async Task SettlePreview_creditor_never_gets_swishPay()
    {
        // Even if the debtor also has a Swish number, the creditor (net > 0) sees no pay button —
        // you never "pay yourself".
        var (factory, hid, debtor, creditor) = await DebtScenarioAsync();
        using var _ = factory;
        await factory.WithDb(async db =>
        {
            var m = await db.Members.FindAsync(debtor);
            m!.SwishNumber = "+46709999999";
            await db.SaveChangesAsync();
        });
        var client = factory.ClientAs(creditor);

        var preview = await client.GetFromJsonAsync<SettlePreviewDto>(
            $"/households/{hid}/settle-preview?person={debtor}", Web);

        Assert.Equal(5000, preview!.NetMinor); // creditor is owed
        Assert.Equal("owesYou", preview.NetLabel);
        Assert.Null(preview.SwishPay);
    }

    [Fact]
    public async Task SettlePreview_no_swishPay_when_creditor_has_no_number()
    {
        var (factory, hid, debtor, creditor) = await DebtScenarioAsync(creditorSwish: null);
        using var _ = factory;
        var client = factory.ClientAs(debtor);

        var preview = await client.GetFromJsonAsync<SettlePreviewDto>(
            $"/households/{hid}/settle-preview?person={creditor}", Web);

        Assert.Equal(-5000, preview!.NetMinor);
        Assert.Null(preview.SwishPay);
    }

    [Fact]
    public async Task SettlePreview_no_swishPay_for_non_sek_household()
    {
        var (factory, hid, debtor, creditor) = await DebtScenarioAsync(currency: "USD");
        using var _ = factory;
        var client = factory.ClientAs(debtor);

        var preview = await client.GetFromJsonAsync<SettlePreviewDto>(
            $"/households/{hid}/settle-preview?person={creditor}", Web);

        Assert.Equal(-5000, preview!.NetMinor); // owes, but Swish has no USD rail
        Assert.Null(preview.SwishPay);
    }

    [Fact]
    public async Task SettlePreview_no_swishPay_when_square()
    {
        var (factory, hid, debtor, creditor) = await DebtScenarioAsync();
        using var _ = factory;
        var client = factory.ClientAs(debtor);

        // Settle the pair so the net goes to zero.
        await client.PostAsJsonAsync($"/households/{hid}/settlements",
            new CreateSettlementRequest(creditor), Web);

        var preview = await client.GetFromJsonAsync<SettlePreviewDto>(
            $"/households/{hid}/settle-preview?person={creditor}", Web);

        Assert.Equal(0, preview!.NetMinor);
        Assert.Equal("square", preview.NetLabel);
        Assert.Null(preview.SwishPay);
    }
}
