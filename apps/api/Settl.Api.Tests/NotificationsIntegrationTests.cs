using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory tests for trust notifications (trust-notifications-v1 spec):
/// mutations emit LedgerEvents, the read projection returns only events concerning the caller
/// (never their own), and the seen-cursor clears unread. Canonical Lönnvägen seed: Du, Sam,
/// Priya are members; Mamma belongs only to Familjen.
/// </summary>
public class NotificationsIntegrationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<SettlApiFactory> SeededAsync()
    {
        var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        return factory;
    }

    private static async Task<NotificationListDto> NotificationsFor(HttpClient client)
    {
        var res = await client.GetFromJsonAsync<NotificationListDto>(
            $"/households/{SeedIds.Lonnvagen}/notifications", Web);
        Assert.NotNull(res);
        return res!;
    }

    /// <summary>Creates a fresh equal expense paid by Du (Sam & Priya each carry a share) and
    /// returns its id — a clean subject the acting member can mutate.</summary>
    private static async Task<Guid> SeedExpense(HttpClient du, string title = "Gemensam pizza", long amount = 9_000)
    {
        var req = new CreateEntryRequest("expense", title, amount, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        return created!.Id;
    }

    [Fact]
    public async Task Deleting_an_entry_notifies_the_other_parties_but_not_the_actor()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await SeedExpense(du);

        var del = await du.DeleteAsync($"/entries/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Sam is a fellow party → gets the notification, unread.
        var sam = await NotificationsFor(factory.ClientAs(SeedIds.Sam));
        Assert.Equal(1, sam.UnreadCount);
        var n = Assert.Single(sam.Items);
        Assert.Equal("entryDeleted", n.Type);
        Assert.Equal(SeedIds.Du, n.ActorMemberId);
        Assert.Equal("Du", n.ActorName);
        Assert.Equal("Gemensam pizza", n.Title);
        Assert.True(n.IsUnread);

        // Du performed it → never notified of their own change.
        var duNotes = await NotificationsFor(du);
        Assert.Equal(0, duNotes.UnreadCount);
        Assert.Empty(duNotes.Items);
    }

    [Fact]
    public async Task Editing_the_amount_records_a_before_after_change()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await SeedExpense(du, "Middag", 9_000);

        // Pure amount change (equal split rescales, but split INTENT is unchanged).
        var update = new UpdateEntryRequest("expense", "Middag", 12_000, null, SeedIds.Du, null, null);
        var put = await du.PutAsJsonAsync($"/entries/{id}", update, Web);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var priya = await NotificationsFor(factory.ClientAs(SeedIds.Priya));
        var n = Assert.Single(priya.Items);
        Assert.Equal("entryEdited", n.Type);
        var change = Assert.Single(n.Changes);      // amount only — no spurious split change
        Assert.Equal("amount", change.Field);
        Assert.Equal("Belopp", change.Label);
        Assert.Equal(Money.FormatKr(9_000), change.Before);
        Assert.Equal(Money.FormatKr(12_000), change.After);
    }

    [Fact]
    public async Task Recording_a_settlement_notifies_the_debtors()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await SeedExpense(du);

        var settle = await du.PostAsync($"/entries/{id}/settlements", null);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);

        var sam = await NotificationsFor(factory.ClientAs(SeedIds.Sam));
        var n = Assert.Single(sam.Items);
        Assert.Equal("settlementRecorded", n.Type);
        Assert.Equal("Gemensam pizza", n.Title);
    }

    [Fact]
    public async Task Changing_a_recurring_amount_notifies_the_household()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Bump the cleaning template's amount (Equal split — a plain amount change validates).
        var patch = await du.PatchAsync($"/recurring/{SeedIds.Cleaning}",
            JsonContent.Create(new UpdateRecurringRequest(null, null, 150_000, null, null, null, null)));
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var sam = await NotificationsFor(factory.ClientAs(SeedIds.Sam));
        var n = Assert.Single(sam.Items);
        Assert.Equal("recurringChanged", n.Type);
        Assert.Equal("Städhjälp", n.Title);
        var change = Assert.Single(n.Changes);
        Assert.Equal("amount", change.Field);
        Assert.Equal(Money.FormatKr(120_000), change.Before);
        Assert.Equal(Money.FormatKr(150_000), change.After);
    }

    [Fact]
    public async Task Marking_seen_clears_unread_but_keeps_the_history()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await SeedExpense(du);
        await du.DeleteAsync($"/entries/{id}");

        var sam = factory.ClientAs(SeedIds.Sam);
        var before = await NotificationsFor(sam);
        Assert.Equal(1, before.UnreadCount);

        var seen = await sam.PostAsync($"/households/{SeedIds.Lonnvagen}/notifications/seen", null);
        Assert.Equal(HttpStatusCode.NoContent, seen.StatusCode);

        var after = await NotificationsFor(sam);
        Assert.Equal(0, after.UnreadCount);
        Assert.Single(after.Items);                 // still in the stream…
        Assert.False(after.Items[0].IsUnread);      // …just no longer unread
    }

    [Fact]
    public async Task Notifications_are_forbidden_for_non_members()
    {
        using var factory = await SeededAsync();
        var mamma = factory.ClientAs(SeedIds.Mamma);   // Familjen only, not Lönnvägen

        var res = await mamma.GetAsync($"/households/{SeedIds.Lonnvagen}/notifications");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
