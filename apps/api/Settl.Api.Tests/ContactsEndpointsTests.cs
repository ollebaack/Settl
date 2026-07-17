using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for contacts &amp; blind SMS invites (ADR-0019).
/// Typing a number never reveals whether it's on Settl (no lookup endpoint exists to test);
/// a contact edge only appears once an invite is accepted (connection-on-accept). SMS delivery
/// is the logging <c>DevSmsSender</c> — the test reads the accept link back from the same
/// in-memory side channel the GET /dev/sms-invites/latest endpoint uses.
/// </summary>
public class ContactsEndpointsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string TokenFrom(string acceptUrl) => acceptUrl.Split("token=")[1];

    [Fact]
    public async Task SmsInvite_returns_201_and_lists_as_pending_without_revealing_registration()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync("/contacts/invites",
            new CreateContactInviteRequest("sms", "070-123 45 67", null, null), Web);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<ContactInviteResultDto>(Web);
        Assert.Equal("sms", result!.Channel);
        Assert.True(result.Delivered);

        // The typed number was normalised to E.164 and stored on the invite row only.
        var stored = await factory.WithDb(db => db.Invites.SingleAsync(i => i.Id == result.Id));
        Assert.Equal("+46701234567", stored.PhoneNumber);
        Assert.Null(stored.Email);
        Assert.Null(stored.HouseholdId);

        var pending = await du.GetFromJsonAsync<List<PendingInviteDto>>("/contacts/pending", Web);
        Assert.Contains(pending!, p => p.Id == result.Id && p.Phone == "+46701234567" && p.Channel == "sms");
    }

    [Fact]
    public async Task SmsInvite_invalid_phone_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync("/contacts/invites",
            new CreateContactInviteRequest("sms", "abc", null, null), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ContactInvite_invalid_channel_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync("/contacts/invites",
            new CreateContactInviteRequest("carrier-pigeon", null, "x@example.com", null), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AcceptingSmsInvite_creates_a_reciprocal_contact_edge()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync("/contacts/invites",
            new CreateContactInviteRequest("sms", "+46705550000", null, null), Web);
        res.EnsureSuccessStatusCode();
        var token = TokenFrom(factory.LastDevSmsInviteAcceptUrl
            ?? throw new InvalidOperationException("No dev SMS invite link recorded"));

        // A brand-new person accepts by supplying their own email (email stays the identity).
        var anon = factory.CreateClient();
        var accept = await anon.PostAsJsonAsync($"/invites/{token}/accept",
            new AcceptInviteRequest("Nykontakt", "nykontakt@example.com", "Password123!"), Web);
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var me = await accept.Content.ReadFromJsonAsync<MeDto>(Web);

        // Du now has the new person as a contact...
        var duContacts = await du.GetFromJsonAsync<List<ContactDto>>("/contacts", Web);
        Assert.Contains(duContacts!, c => c.MemberId == me!.Id && c.Name == "Nykontakt");

        // ...and the new person (now signed in on `anon`) has Du.
        var theirContacts = await anon.GetFromJsonAsync<List<ContactDto>>("/contacts", Web);
        Assert.Contains(theirContacts!, c => c.MemberId == SeedIds.Du);

        // Contact-only invite: no household was joined, so no shared book yet.
        Assert.Equal(0, duContacts!.Single(c => c.MemberId == me!.Id).SharedHouseholdCount);

        // The raw number is scrubbed off the invite once accepted (ADR-0019 / GDPR).
        var invite = await factory.WithDb(db => db.Invites.SingleAsync(i => i.AcceptedAt != null));
        Assert.Null(invite.PhoneNumber);
    }

    [Fact]
    public async Task ContactInvite_is_rate_limited_per_member()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // The fixed window permits 10 sends; the 11th is throttled (429), guarding the SMS budget.
        HttpStatusCode last = HttpStatusCode.OK;
        for (var i = 0; i < 11; i++)
        {
            var res = await du.PostAsJsonAsync("/contacts/invites",
                new CreateContactInviteRequest("sms", $"+4670555{i:D4}", null, null), Web);
            last = res.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task InvitableContacts_reports_member_vs_invitable_status()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // Du's saved contacts: Sam & Priya (both in Lönnvägen) and Mamma (only in Familjen).
        await factory.WithDb(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            db.Contacts.AddRange(
                new Contact { OwnerMemberId = SeedIds.Du, ContactMemberId = SeedIds.Sam, CreatedAt = now },
                new Contact { OwnerMemberId = SeedIds.Du, ContactMemberId = SeedIds.Priya, CreatedAt = now },
                new Contact { OwnerMemberId = SeedIds.Du, ContactMemberId = SeedIds.Mamma, CreatedAt = now });
            await db.SaveChangesAsync();
        });

        var du = factory.ClientAs(SeedIds.Du);
        var invitable = await du.GetFromJsonAsync<List<InvitableContactDto>>(
            $"/households/{SeedIds.Lonnvagen}/invitable-contacts", Web);
        Assert.NotNull(invitable);

        Assert.Equal("member", invitable.Single(c => c.MemberId == SeedIds.Sam).Status);
        Assert.Equal("member", invitable.Single(c => c.MemberId == SeedIds.Priya).Status);
        Assert.Equal("invitable", invitable.Single(c => c.MemberId == SeedIds.Mamma).Status);
    }

    [Fact]
    public async Task InviteContactToHousehold_sends_invite_and_marks_contact_pending()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // Du's contact Mamma is not in Lönnvägen — so she's invitable there.
        await factory.WithDb(async db =>
        {
            db.Contacts.Add(new Contact
            { OwnerMemberId = SeedIds.Du, ContactMemberId = SeedIds.Mamma, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var du = factory.ClientAs(SeedIds.Du);
        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invite-contact",
            new InviteContactRequest(SeedIds.Mamma), Web);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        // A household email invite bound to Mamma's address now exists.
        var invited = await factory.WithDb(db => db.Invites
            .AnyAsync(i => i.HouseholdId == SeedIds.Lonnvagen && i.Email == "mamma@settl.dev" && i.AcceptedAt == null));
        Assert.True(invited);

        var invitable = await du.GetFromJsonAsync<List<InvitableContactDto>>(
            $"/households/{SeedIds.Lonnvagen}/invitable-contacts", Web);
        Assert.Equal("pending", invitable!.Single(c => c.MemberId == SeedIds.Mamma).Status);
    }

    [Fact]
    public async Task InviteContactToHousehold_rejects_a_non_contact()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Priya is a household member but not a saved contact of Du — must not be invitable this way.
        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invite-contact",
            new InviteContactRequest(SeedIds.Priya), Web);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task InviteContactToHousehold_conflicts_when_already_a_member()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // Sam is both Du's contact and already a Lönnvägen member.
        await factory.WithDb(async db =>
        {
            db.Contacts.Add(new Contact
            { OwnerMemberId = SeedIds.Du, ContactMemberId = SeedIds.Sam, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var du = factory.ClientAs(SeedIds.Du);
        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/invite-contact",
            new InviteContactRequest(SeedIds.Sam), Web);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task PutMe_stores_phone_as_unverified_e164_and_clears_it()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // The member's single number (ADR-0026) is written through PUT /me alongside name/avatar.
        var set = await du.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, Phone: "073-555 12 34"), Web);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var me = await set.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Equal("+46735551234", me!.Phone);
        Assert.False(me.PhoneVerified);

        var cleared = await du.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, Phone: null), Web);
        var after = await cleared.Content.ReadFromJsonAsync<MeDto>(Web);
        Assert.Null(after!.Phone);
    }

    [Fact]
    public async Task PutMe_invalid_phone_returns_400()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PutAsJsonAsync("/me", new UpdateMeRequest("Du", null, Phone: "nope"), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
