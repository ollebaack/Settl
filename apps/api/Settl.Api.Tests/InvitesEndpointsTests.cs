using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for household invites (ADR-0011). The raw
/// accept token is never persisted (only its hash is) or returned by any endpoint's JSON —
/// same as a real inbox, the only way to get it is the emailed link. GET /dev/invites/latest
/// is Development-only and the test host runs "Testing", so these tests instead read the
/// link straight from <see cref="SettlApiFactory.LastDevInviteAcceptUrl"/>, the same
/// in-memory side channel that endpoint reads from.
/// </summary>
public class InvitesEndpointsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    private static string TokenFrom(string acceptUrl) => acceptUrl.Split("token=")[1];

    [Fact]
    public async Task CreateInvite_by_nonmember_returns_403()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        // Mamma is a member of Familjen, not Lönnvägen.
        var mamma = factory.ClientAs(SeedIds.Mamma);

        var res = await mamma.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreateInvite_by_member_returns_201_and_lists_as_pending()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("Ny@Example.com"), Web);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var invite = await res.Content.ReadFromJsonAsync<InviteDto>(Web);
        Assert.Equal("ny@example.com", invite!.Email); // normalized

        var pending = await du.GetFromJsonAsync<List<InviteDto>>(
            $"/households/{SeedIds.Lonnvagen}/invites", Web);
        Assert.Contains(pending!, i => i.Id == invite.Id);
    }

    [Fact]
    public async Task CreateInvite_invalid_email_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("not-an-email"), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PreviewInvite_reports_household_inviter_and_no_existing_account()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web);
        var token = TokenForInvite(factory);

        var preview = await factory.CreateClient().GetFromJsonAsync<InvitePreviewDto>($"/invites/{token}", Web);
        Assert.Equal("Lönnvägen 3", preview!.HouseholdName);
        Assert.Equal("Du", preview.InviterName);
        Assert.Equal("ny@example.com", preview.Email);
        Assert.False(preview.HasAccount);
    }

    [Fact]
    public async Task AcceptInvite_new_email_creates_account_and_joins_household()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web);
        var token = TokenForInvite(factory);

        var anon = factory.CreateClient();
        var accept = await anon.PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Ny Person", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var member = await accept.Content.ReadFromJsonAsync<MemberDto>(Web);
        Assert.Equal("Ny Person", member!.Name);

        // The accepting client is now signed in as the new member.
        var me = await anon.GetFromJsonAsync<MemberDto>("/me", Web);
        Assert.Equal(member.Id, me!.Id);

        var members = await anon.GetFromJsonAsync<List<MemberDto>>(
            $"/households/{SeedIds.Lonnvagen}/members", Web);
        Assert.Contains(members!, m => m.Id == member.Id);
    }

    [Fact]
    public async Task AcceptInvite_new_email_without_password_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web);
        var token = TokenForInvite(factory);

        var res = await factory.CreateClient().PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Ny Person", null), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Lösenord krävs", await DetailAsync(res));
    }

    [Fact]
    public async Task AcceptInvite_existing_account_requires_login_as_that_email_first()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // Priya already has an account (seeded) and is invited to Familjen.
        var du = factory.ClientAs(SeedIds.Du);
        await du.PostAsJsonAsync($"/households/{SeedIds.Familjen}/invites",
            new CreateInviteRequest("priya@settl.dev"), Web);
        var token = TokenForInvite(factory);

        // Preview confirms an account already exists.
        var preview = await factory.CreateClient().GetFromJsonAsync<InvitePreviewDto>($"/invites/{token}", Web);
        Assert.True(preview!.HasAccount);

        // Accepting anonymously is rejected.
        var anonAccept = await factory.CreateClient().PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest(null, null), Web);
        Assert.Equal(HttpStatusCode.Unauthorized, anonAccept.StatusCode);

        // Accepting while logged in as Priya succeeds and adds the membership.
        var priya = factory.ClientAs(SeedIds.Priya);
        var accept = await priya.PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest(null, null), Web);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);

        var members = await priya.GetFromJsonAsync<List<MemberDto>>(
            $"/households/{SeedIds.Familjen}/members", Web);
        Assert.Contains(members!, m => m.Id == SeedIds.Priya);
    }

    [Fact]
    public async Task AcceptInvite_expired_token_returns_404()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var invite = await (await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web)).Content.ReadFromJsonAsync<InviteDto>(Web);
        var token = TokenForInvite(factory);

        await factory.WithDb(async db =>
        {
            var i = await db.Invites.SingleAsync(x => x.Id == invite!.Id);
            i.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        });

        var res = await factory.CreateClient().PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Ny", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task AcceptInvite_already_accepted_returns_404_on_second_attempt()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invites",
            new CreateInviteRequest("ny@example.com"), Web);
        var token = TokenForInvite(factory);

        var first = await factory.CreateClient().PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Ny Person", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await factory.CreateClient().PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Ny Person", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    private static string TokenForInvite(SettlApiFactory factory) =>
        TokenFrom(factory.LastDevInviteAcceptUrl ?? throw new InvalidOperationException("No dev invite link recorded"));
}
