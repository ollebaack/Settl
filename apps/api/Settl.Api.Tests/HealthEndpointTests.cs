using System.Net;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

// Uses SettlApiFactory (isolated in-memory DB, Testing environment) rather than a bare
// WebApplicationFactory<Program> — Program.cs always migrates a real Postgres connection
// at startup (ADR-0010), which a bare factory has no way to provide.
public class HealthEndpointTests : IClassFixture<SettlApiFactory>
{
    private readonly SettlApiFactory _factory;

    public HealthEndpointTests(SettlApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
