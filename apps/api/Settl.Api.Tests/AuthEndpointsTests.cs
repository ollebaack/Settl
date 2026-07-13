using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>WebApplicationFactory integration tests for /auth/register, /auth/login, /auth/logout.</summary>
public class AuthEndpointsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    [Fact]
    public async Task Register_creates_account_and_signs_in()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "alex@example.com", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<MemberDto>(Web);
        Assert.Equal("Alex", created!.Name);

        // The registering client is signed in — /me works without a separate login.
        var me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meDto = await me.Content.ReadFromJsonAsync<MemberDto>(Web);
        Assert.Equal(created.Id, meDto!.Id);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_400()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "dup@example.com", "Password123!"), Web);
        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex Igen", "dup@example.com", "Password456!"), Web);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("E-postadressen används redan", await DetailAsync(res));
    }

    [Fact]
    public async Task Register_blank_name_returns_400()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("   ", "blank@example.com", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Namn krävs", await DetailAsync(res));
    }

    [Fact]
    public async Task Register_weak_password_returns_400()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "weak@example.com", "123"), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_with_seeded_credentials_succeeds()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest("du@settl.dev", SeedIds.DevPassword), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest("du@settl.dev", "wrong-password"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_unknown_email_returns_401()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest("nobody@example.com", "whatever123"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Logout_clears_session()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.ClientAs(SeedIds.Du);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/me")).StatusCode);

        var logout = await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var meAfter = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfter.StatusCode);
    }
}
