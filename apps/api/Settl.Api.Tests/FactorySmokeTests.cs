using System.Net;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Proves the shared test harness works end-to-end: isolated in-memory DB, the X-Settl-User
/// identity header, and canonical seeding. Per-slice test suites build on <see cref="SettlApiFactory"/>.
/// </summary>
public class FactorySmokeTests
{
    [Fact]
    public async Task Harness_boots_seeds_and_serves_authenticated_reads()
    {
        using var factory = new SettlApiFactory();

        // 1. App boots; health is reachable without any DB dependency.
        var anon = factory.CreateClient();
        var health = await anon.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // 2. Seed the canonical fixture into this factory's isolated DB.
        await factory.SeedCanonicalAsync();

        // 3. Default client acts as the seeded member "Du".
        var meResponse = await anon.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        using var me = JsonDocument.Parse(await meResponse.Content.ReadAsStringAsync());
        Assert.Equal("Du", me.RootElement.GetProperty("name").GetString());
        Assert.Equal(SeedIds.Du, me.RootElement.GetProperty("id").GetGuid());

        // 4. Acting as Du, the Lönnvägen household surfaces at least one nudge.
        var du = factory.ClientAs(SeedIds.Du);
        var nudgesResponse = await du.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges");
        Assert.Equal(HttpStatusCode.OK, nudgesResponse.StatusCode);
        using var nudges = JsonDocument.Parse(await nudgesResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, nudges.RootElement.ValueKind);
        Assert.True(nudges.RootElement.GetArrayLength() >= 1, "expected at least one nudge");
    }
}
