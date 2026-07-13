using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>WebApplicationFactory integration tests for /auth/register, /auth/login,
/// /auth/logout, email verification, and password reset.</summary>
public class AuthEndpointsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    private static (string userId, string token) ParseLink(string url)
    {
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(url).Query);
        return (query["userId"].ToString(), query["token"].ToString());
    }

    [Fact]
    public async Task Register_creates_account_and_signs_in()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "alex@example.com", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal("Alex", created!.Name);
        Assert.False(created.EmailConfirmed);

        // The registering client is signed in — /me works without a separate login, even
        // though the email isn't confirmed yet (AuthenticatedOnly, not the fallback policy).
        var me = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meDto = await me.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal(created.Id, meDto!.Id);
        Assert.False(meDto.EmailConfirmed);
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
    public async Task Unconfirmed_account_is_blocked_from_confirmed_only_endpoints()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "gated@example.com", "Password123!"), Web);

        // Signed in, but unconfirmed — the fallback policy's EmailConfirmedRequirement
        // fails, which the cookie scheme turns into 403 (not 401 — there IS a session).
        var households = await client.GetAsync("/households");
        Assert.Equal(HttpStatusCode.Forbidden, households.StatusCode);

        var confirmUrl = factory.LastDevVerificationUrl ?? throw new InvalidOperationException("No verification link recorded");
        var (userId, token) = ParseLink(confirmUrl);
        var confirm = await client.PostAsJsonAsync("/auth/confirm-email",
            new { UserId = Guid.Parse(userId), Token = token }, Web);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var householdsAfter = await client.GetAsync("/households");
        Assert.Equal(HttpStatusCode.OK, householdsAfter.StatusCode);

        var me = await client.GetFromJsonAsync<MeDto>("/me", Web);
        Assert.True(me!.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_invalid_token_returns_400()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "badtoken@example.com", "Password123!"), Web);
        var created = await register.Content.ReadFromJsonAsync<MeDto>(Web);

        var res = await client.PostAsJsonAsync("/auth/confirm-email",
            new { UserId = created!.Id, Token = "not-a-real-token" }, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_already_confirmed_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du); // seeded members are already EmailConfirmed

        var res = await du.PostAsync("/auth/resend-verification", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_unconfirmed_sends_a_fresh_link()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/auth/register",
            new RegisterRequest("Alex", "resend@example.com", "Password123!"), Web);
        var firstLink = factory.LastDevVerificationUrl;

        var res = await client.PostAsync("/auth/resend-verification", null);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.NotEqual(firstLink, factory.LastDevVerificationUrl);
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

    [Fact]
    public async Task ForgotPassword_unknown_email_returns_204_without_revealing_anything()
    {
        using var factory = new SettlApiFactory();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/forgot-password",
            new ForgotPasswordRequest("nobody@example.com"), Web);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Null(factory.LastDevPasswordResetUrl);
    }

    [Fact]
    public async Task ForgotPassword_then_ResetPassword_signs_in_with_new_password()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var anon = factory.CreateClient();

        var forgot = await anon.PostAsJsonAsync("/auth/forgot-password",
            new ForgotPasswordRequest("du@settl.dev"), Web);
        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);

        var resetUrl = factory.LastDevPasswordResetUrl ?? throw new InvalidOperationException("No reset link recorded");
        var (userId, token) = ParseLink(resetUrl);

        var reset = await anon.PostAsJsonAsync("/auth/reset-password",
            new { UserId = Guid.Parse(userId), Token = token, NewPassword = "NewPassword123!" }, Web);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // The resetting client is signed in as part of the reset.
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/me")).StatusCode);

        // Old password no longer works; new one does (from a fresh, unauthenticated client).
        var freshClient = factory.CreateClient();
        var oldLogin = await freshClient.PostAsJsonAsync("/auth/login",
            new LoginRequest("du@settl.dev", SeedIds.DevPassword), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await freshClient.PostAsJsonAsync("/auth/login",
            new LoginRequest("du@settl.dev", "NewPassword123!"), Web);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_invalid_token_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var client = factory.CreateClient();

        var res = await client.PostAsJsonAsync("/auth/reset-password",
            new { UserId = SeedIds.Du, Token = "not-a-real-token", NewPassword = "NewPassword123!" }, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
