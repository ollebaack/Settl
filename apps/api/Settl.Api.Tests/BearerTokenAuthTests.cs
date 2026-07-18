using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>WebApplicationFactory integration tests for the native client's bearer-token auth
/// (ADR-0005): POST /auth/token issuance, POST /auth/token/refresh rotation, and that a bearer
/// token authorizes the same endpoints the cookie does — including the confirmed-email fallback
/// policy — without any cookie present.</summary>
public class BearerTokenAuthTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The BearerToken handler's AccessTokenResponse shape ({ tokenType, accessToken,
    /// expiresIn, refreshToken }). Redeclared here rather than referencing the framework type so
    /// the test asserts the wire contract the native client will actually parse.</summary>
    private sealed record TokenResponse(string TokenType, string AccessToken, int ExpiresIn, string RefreshToken);

    private static async Task<TokenResponse> IssueTokenAsync(HttpClient client, string email, string password)
    {
        var res = await client.PostAsJsonAsync("/auth/token", new LoginRequest(email, password), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var token = await res.Content.ReadFromJsonAsync<TokenResponse>(Web);
        Assert.NotNull(token);
        Assert.Equal("Bearer", token!.TokenType);
        Assert.False(string.IsNullOrEmpty(token.AccessToken));
        Assert.False(string.IsNullOrEmpty(token.RefreshToken));
        return token;
    }

    /// <summary>A cookie-free client that authenticates purely by bearer token, so a passing
    /// request proves the token — not a stray cookie — did the work.</summary>
    private static HttpClient BearerClient(SettlApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task IssueToken_with_seeded_credentials_returns_a_token_that_authorizes_me()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        var token = await IssueTokenAsync(factory.CreateClient(), "du@settl.dev", SeedIds.DevPassword);

        var me = await BearerClient(factory, token.AccessToken).GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meDto = await me.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal("du@settl.dev", meDto!.Email);
    }

    [Fact]
    public async Task IssueToken_wrong_password_returns_401()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        var res = await factory.CreateClient().PostAsJsonAsync("/auth/token",
            new LoginRequest("du@settl.dev", "wrong-password"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task IssueToken_unknown_email_returns_401()
    {
        using var factory = new SettlApiFactory();

        var res = await factory.CreateClient().PostAsJsonAsync("/auth/token",
            new LoginRequest("nobody@example.com", "whatever123"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Bearer_token_satisfies_the_confirmed_email_fallback_policy()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync(); // seeded members are EmailConfirmed

        var token = await IssueTokenAsync(factory.CreateClient(), "du@settl.dev", SeedIds.DevPassword);

        // /households runs under the fallback policy (auth + confirmed email). A bearer token
        // clearing it proves the policy accepts the bearer scheme, not just the cookie.
        var households = await BearerClient(factory, token.AccessToken).GetAsync("/households");
        Assert.Equal(HttpStatusCode.OK, households.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // A fresh client carries neither cookie nor bearer token.
        var me = await factory.CreateClient().GetAsync("/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_returns_a_new_pair_that_still_authorizes()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        var first = await IssueTokenAsync(factory.CreateClient(), "du@settl.dev", SeedIds.DevPassword);

        var refreshRes = await factory.CreateClient().PostAsJsonAsync("/auth/token/refresh",
            new RefreshTokenRequest(first.RefreshToken), Web);
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);
        var refreshed = await refreshRes.Content.ReadFromJsonAsync<TokenResponse>(Web);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrEmpty(refreshed!.AccessToken));

        var me = await BearerClient(factory, refreshed.AccessToken).GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_a_garbage_token_returns_401()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        var res = await factory.CreateClient().PostAsJsonAsync("/auth/token/refresh",
            new RefreshTokenRequest("not-a-real-refresh-token"), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
