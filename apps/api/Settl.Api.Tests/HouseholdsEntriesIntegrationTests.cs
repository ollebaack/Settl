using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for the Households + Entries endpoint groups,
/// driven against the canonical seed (SeedIds / DbInitializer). Each test owns an isolated
/// in-memory DB (fresh factory), so mutating tests (POST / settle / delete) never leak.
///
/// Expected balances are derived by hand from the canonical Lönnvägen 3 fixture (Du, Sam,
/// Priya in membership order) and cross-checked against the pure BalanceCalculator rules:
///   NetWith(Du,Sam)  = -96000 -28800 +14967 +12000 -5634  = -103467  (Du owes Sam)
///   NetWith(Du,Priya)= -20000 -18000 +14966               =  -23034  (Du owes Priya)
///   overall(Du)      = -126501  → label "owe"
/// e6 (Städmaterial) and e3 (Hyra — juli) are fully settled in the seed, so 7 of the 9
/// Lönnvägen entries have open debts → openCount = 7.
/// </summary>
public class HouseholdsEntriesIntegrationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<SettlApiFactory> SeededAsync()
    {
        var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        return factory;
    }

    private static async Task<string> DetailAsync(HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("detail").GetString() ?? "";
    }

    // ---------- Meta ----------

    [Fact]
    public async Task GetMe_returns_acting_member()
    {
        using var factory = await SeededAsync();
        var sam = factory.ClientAs(SeedIds.Sam);

        var res = await sam.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var me = await res.Content.ReadFromJsonAsync<MemberDto>(Web);
        Assert.NotNull(me);
        Assert.Equal(SeedIds.Sam, me!.Id);
        Assert.Equal("Sam", me.Name);
        Assert.Equal("#f0dcc3", me.AvatarColor);
    }

    // ---------- Households ----------

    [Fact]
    public async Task GetHouseholds_returns_both_of_Dus_households_with_net_and_label()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.GetAsync("/households");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var items = await res.Content.ReadFromJsonAsync<List<HouseholdListItemDto>>(Web);
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);

        var lonn = items.Single(h => h.Id == SeedIds.Lonnvagen);
        Assert.Equal("Lönnvägen 3", lonn.Name);
        Assert.Equal("SEK", lonn.Currency);
        Assert.Equal(new[] { "Du", "Sam", "Priya" }, lonn.MemberNames.ToArray());
        Assert.Equal(-126501, lonn.NetMinor);
        Assert.Equal("owe", lonn.NetLabel);

        var fam = items.Single(h => h.Id == SeedIds.Familjen);
        Assert.Equal("Familjen", fam.Name);
        Assert.Equal(266, fam.NetMinor);   // Du is net owed +266 öre in Familjen
        Assert.Equal("owed", fam.NetLabel);
    }

    [Fact]
    public async Task GetHousehold_returns_members_in_membership_order()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var hh = await du.GetFromJsonAsync<HouseholdDto>($"/households/{SeedIds.Lonnvagen}", Web);
        Assert.NotNull(hh);
        Assert.Equal("Lönnvägen 3", hh!.Name);
        Assert.Equal("SEK", hh.Currency);
        Assert.Equal(3, hh.Members.Count);
        Assert.Equal(new[] { "Du", "Sam", "Priya" }, hh.Members.Select(m => m.Name).ToArray());
        Assert.Equal(SeedIds.Du, hh.Members[0].Id);
        Assert.Equal("#dfe6cf", hh.Members[0].AvatarColor);
    }

    [Fact]
    public async Task GetHousehold_unknown_returns_404()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.GetAsync($"/households/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetHouseholdMembers_returns_ordered_members()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var members = await du.GetFromJsonAsync<List<MemberDto>>(
            $"/households/{SeedIds.Lonnvagen}/members", Web);
        Assert.NotNull(members);
        Assert.Equal(new[] { "Du", "Sam", "Priya" }, members!.Select(m => m.Name).ToArray());
    }

    [Fact]
    public async Task GetSummary_reports_net_openCount_people_and_upcoming()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var summary = await du.GetFromJsonAsync<HouseholdSummaryDto>(
            $"/households/{SeedIds.Lonnvagen}/summary", Web);
        Assert.NotNull(summary);

        // Overall net (Du owes) and label.
        Assert.Equal(-126501, summary!.OverallNetMinor);
        Assert.Equal("owe", summary.NetLabel);

        // 7 of 9 Lönnvägen entries still have open debts (e6, e3 are settled).
        Assert.Equal(7, summary.OpenCount);

        // People = the two other members, with per-person net + relation.
        Assert.Equal(2, summary.People.Count);
        var sam = summary.People.Single(p => p.MemberId == SeedIds.Sam);
        Assert.Equal(-103467, sam.NetMinor);
        Assert.Equal("youOwe", sam.Relation);
        Assert.Equal("Sam", sam.Name);

        var priya = summary.People.Single(p => p.MemberId == SeedIds.Priya);
        Assert.Equal(-23034, priya.NetMinor);
        Assert.Equal("youOwe", priya.Relation);

        // Upcoming: active templates within 30 days, soonest first, max 4.
        Assert.Equal(4, summary.Upcoming.Count);
        Assert.All(summary.Upcoming, u => Assert.True(u.DaysUntil <= 30, "within 30 days"));
        for (var i = 1; i < summary.Upcoming.Count; i++)
            Assert.True(summary.Upcoming[i - 1].DaysUntil <= summary.Upcoming[i].DaysUntil,
                "upcoming must be soonest-first");

        var soonest = summary.Upcoming[0];
        Assert.Equal(SeedIds.Rent, soonest.RecurringId);
        Assert.Equal("Hyra", soonest.Title);
        Assert.True(soonest.DaysUntil <= 5, "rent posts within 5 days in the seed");
        Assert.Equal(900_000, soonest.YourShareMinor);  // Du's amount-split share of rent
        Assert.Equal(2_400_000, soonest.AmountMinor);
    }

    // ---------- Households: create ----------

    [Fact]
    public async Task CreateHousehold_always_includes_acting_user_first_even_when_unlisted()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new CreateHouseholdRequest("Sommarstugan", null);
        var post = await du.PostAsJsonAsync("/households", req, Web);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var created = await post.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        Assert.NotNull(created);
        Assert.Equal("Sommarstugan", created!.Name);
        Assert.Equal("SEK", created.Currency);
        Assert.Single(created.Members);
        Assert.Equal(SeedIds.Du, created.Members[0].Id);

        // The household is now visible to the acting user via the list endpoint.
        var list = await du.GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.Contains(list!, h => h.Id == created.Id && h.Name == "Sommarstugan");
    }

    [Fact]
    public async Task CreateHousehold_blank_name_returns_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new CreateHouseholdRequest("   ", null);
        var res = await du.PostAsJsonAsync("/households", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Namn krävs", await DetailAsync(res));
    }

    // ---------- Entries: reads ----------

    [Fact]
    public async Task GetEntries_returns_all_household_entries_sorted_date_desc()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var entries = await du.GetFromJsonAsync<List<EntryDto>>(
            $"/households/{SeedIds.Lonnvagen}/entries", Web);
        Assert.NotNull(entries);
        Assert.Equal(9, entries!.Count);

        // Default sort is date descending.
        Assert.Equal("Begagnad soffa", entries[0].Title);
        for (var i = 1; i < entries.Count; i++)
            Assert.True(entries[i - 1].Date >= entries[i].Date, "entries must be date_desc");
    }

    [Fact]
    public async Task GetEntries_type_filters_split_by_kind()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var expenses = await du.GetFromJsonAsync<List<EntryDto>>(
            $"/households/{SeedIds.Lonnvagen}/entries?type=expense", Web);
        // 4 plain expenses + the 2 former IOUs, now "Allt på en" amount-split expenses (ADR-0020).
        Assert.Equal(6, expenses!.Count);
        Assert.All(expenses, e => Assert.Equal("expense", e.Type));

        var recurring = await du.GetFromJsonAsync<List<EntryDto>>(
            $"/households/{SeedIds.Lonnvagen}/entries?type=recurring", Web);
        Assert.Equal(3, recurring!.Count);
        Assert.All(recurring, e => Assert.Equal("recurringPost", e.Type));
    }

    [Fact]
    public async Task GetEntries_limit_takes_top_n_after_date_desc()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var top3 = await du.GetFromJsonAsync<List<EntryDto>>(
            $"/households/{SeedIds.Lonnvagen}/entries?limit=3&sort=date_desc", Web);
        Assert.Equal(3, top3!.Count);
        Assert.Equal(
            new[] { "Begagnad soffa", "Matinköp — storhandling", "Konsertbiljett" },
            top3.Select(e => e.Title).ToArray());
    }

    // ---------- Entries: create ----------

    [Fact]
    public async Task PostEqualExpense_freezes_shares_with_deterministic_remainder()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // 10000 öre split 3 ways → [3334, 3333, 3333] in membership order (Du first).
        var req = new CreateEntryRequest("expense", null, 10_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.NotNull(created);

        // Assert the FROZEN shares via a fresh GET /entries/{id}.
        var entry = await du.GetFromJsonAsync<EntryDto>($"/entries/{created!.Id}", Web);
        Assert.NotNull(entry);
        Assert.Equal("expense", entry!.Type);
        Assert.Equal("equal", entry.SplitMode);
        Assert.Equal("Utan titel", entry.Title);     // default expense title
        Assert.Equal(3, entry.Shares.Count);

        var du3 = entry.Shares.Single(s => s.MemberId == SeedIds.Du);
        var sam3 = entry.Shares.Single(s => s.MemberId == SeedIds.Sam);
        var priya3 = entry.Shares.Single(s => s.MemberId == SeedIds.Priya);
        Assert.Equal(3334, du3.ShareMinor);
        Assert.Equal(3333, sam3.ShareMinor);
        Assert.Equal(3333, priya3.ShareMinor);
        Assert.True(du3.IsPayer);
        Assert.False(sam3.IsPayer);
        Assert.Equal(10_000, du3.ShareMinor + sam3.ShareMinor + priya3.ShareMinor);
    }

    [Fact]
    public async Task PostPercentExpense_freezes_percent_shares()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var split = new SplitInput("percent",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 50m, [SeedIds.Sam] = 30m, [SeedIds.Priya] = 20m });
        var req = new CreateEntryRequest("expense", "Delad middag", 10_000, null, SeedIds.Sam, split);

        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);

        var entry = await du.GetFromJsonAsync<EntryDto>($"/entries/{created!.Id}", Web);
        Assert.Equal("percent", entry!.SplitMode);
        Assert.Equal(5000, entry.Shares.Single(s => s.MemberId == SeedIds.Du).ShareMinor);
        Assert.Equal(3000, entry.Shares.Single(s => s.MemberId == SeedIds.Sam).ShareMinor);
        Assert.Equal(2000, entry.Shares.Single(s => s.MemberId == SeedIds.Priya).ShareMinor);
        Assert.True(entry.Shares.Single(s => s.MemberId == SeedIds.Sam).IsPayer);
    }

    [Fact]
    public async Task PostAmountExpense_freezes_amount_shares()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var split = new SplitInput("amount",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 4000m, [SeedIds.Sam] = 3000m, [SeedIds.Priya] = 3000m });
        var req = new CreateEntryRequest("expense", "Delat kvitto", 10_000, null, SeedIds.Du, split);

        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);

        var entry = await du.GetFromJsonAsync<EntryDto>($"/entries/{created!.Id}", Web);
        Assert.Equal("amount", entry!.SplitMode);
        Assert.Equal(4000, entry.Shares.Single(s => s.MemberId == SeedIds.Du).ShareMinor);
        Assert.Equal(3000, entry.Shares.Single(s => s.MemberId == SeedIds.Sam).ShareMinor);
        Assert.Equal(3000, entry.Shares.Single(s => s.MemberId == SeedIds.Priya).ShareMinor);
    }

    [Fact]
    public async Task PostEntry_rejects_the_removed_iou_type()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // "iou" was removed (ADR-0020): one-owes-all is now the "Allt på en" amount split.
        var req = new CreateEntryRequest("iou", null, 5_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }

    [Fact]
    public async Task PostExpense_with_nonpositive_amount_returns_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new CreateEntryRequest("expense", "Noll", 0, null, SeedIds.Du, null);
        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Ange ett belopp först", await DetailAsync(res));
    }

    [Fact]
    public async Task PostPercentExpense_not_summing_to_100_returns_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var split = new SplitInput("percent",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 50m, [SeedIds.Sam] = 30m, [SeedIds.Priya] = 10m }); // 90
        var req = new CreateEntryRequest("expense", "Fel procent", 10_000, null, SeedIds.Sam, split);

        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Procenten måste bli 100", await DetailAsync(res));
    }

    [Fact]
    public async Task PostAmountExpense_off_by_more_than_tolerance_returns_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Values sum to 9000 but amount is 10000 → off by 1000 öre (> 5 öre tolerance).
        var split = new SplitInput("amount",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 4000m, [SeedIds.Sam] = 3000m, [SeedIds.Priya] = 2000m });
        var req = new CreateEntryRequest("expense", "Fel summa", 10_000, null, SeedIds.Du, split);

        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal($"Delningen måste bli {Money.FormatKr(10_000)}", await DetailAsync(res));
    }

    [Fact]
    public async Task PostExpense_with_whole_share_on_payer_returns_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Everything assigned to the payer, nothing to anyone else → balance-neutral,
        // "ingen andel" for the rest. A shared expense must include someone else.
        var split = new SplitInput("percent",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 100m, [SeedIds.Sam] = 0m, [SeedIds.Priya] = 0m });
        var req = new CreateEntryRequest("expense", "Bara jag", 10_000, null, SeedIds.Du, split);

        var res = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Lägg till någon att dela med", await DetailAsync(res));
    }

    [Fact]
    public async Task PostExpense_payer_only_is_allowed_in_a_solo_household()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Fresh solo household (only Du): logging an own expense must still be allowed,
        // even though the payer is the sole share-holder.
        var hh = await du.PostAsJsonAsync("/households", new CreateHouseholdRequest("Solo", null), Web);
        var solo = await hh.Content.ReadFromJsonAsync<HouseholdDto>(Web);

        var req = new CreateEntryRequest("expense", "Eget", 5_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{solo!.Id}/entries", req, Web);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
    }

    // ---------- Entries: category ----------

    [Fact]
    public async Task PostExpense_infers_category_from_title_keyword()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new CreateEntryRequest("expense", "ICA storhandling", 5_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("groceries", created!.Category);
    }

    [Fact]
    public async Task PostExpense_prefers_cleaning_over_groceries_when_both_keywords_present()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // "Städmaterial" contains "städ" (Cleaning) — must not match "mat" (Groceries) first.
        var req = new CreateEntryRequest("expense", "Städmaterial", 500, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("cleaning", created!.Category);
    }

    [Fact]
    public async Task PostExpense_with_no_keyword_match_defaults_to_other()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new CreateEntryRequest("expense", "Blaha blaha", 500, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", req, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("other", created!.Category);
    }

    [Fact]
    public async Task PutEntry_can_override_category_without_touching_frozen_percent_shares()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var split = new SplitInput("percent",
            new Dictionary<Guid, decimal> { [SeedIds.Du] = 50m, [SeedIds.Sam] = 30m, [SeedIds.Priya] = 20m });
        var createReq = new CreateEntryRequest("expense", "Middag", 10_000, null, SeedIds.Sam, split);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", createReq, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("restaurant", created!.Category); // "middag" keyword

        // Category-only edit: same fields, no split supplied, explicit category override.
        var updateReq = new UpdateEntryRequest(
            "expense", "Middag", 10_000, null, SeedIds.Sam, null, "gift");
        var put = await du.PutAsJsonAsync($"/entries/{created.Id}", updateReq, Web);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var entry = await du.GetFromJsonAsync<EntryDto>($"/entries/{created.Id}", Web);
        Assert.Equal("gift", entry!.Category);
        Assert.Equal("percent", entry.SplitMode);       // untouched — not reset to equal
        Assert.Equal(5000, entry.Shares.Single(s => s.MemberId == SeedIds.Du).ShareMinor);
        Assert.Equal(3000, entry.Shares.Single(s => s.MemberId == SeedIds.Sam).ShareMinor);
        Assert.Equal(2000, entry.Shares.Single(s => s.MemberId == SeedIds.Priya).ShareMinor);
    }

    [Fact]
    public async Task PutEntry_without_category_override_keeps_stored_category_even_if_title_changes()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var createReq = new CreateEntryRequest("expense", "Hyra", 5_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", createReq, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("rent", created!.Category);

        // Title now matches "mat" (Groceries) — but with no category override, the stored
        // category should NOT silently reclassify (no retroactive keyword matching).
        var updateReq = new UpdateEntryRequest(
            "expense", "Matinköp", 5_000, null, SeedIds.Du, null, null);
        var put = await du.PutAsJsonAsync($"/entries/{created.Id}", updateReq, Web);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var entry = await du.GetFromJsonAsync<EntryDto>($"/entries/{created.Id}", Web);
        Assert.Equal("rent", entry!.Category);
    }

    // ---------- Entries: settle / lock / reopen ----------

    [Fact]
    public async Task SettleReopenLifecycle_locks_then_unlocks_entry()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Fresh equal expense paid by Du: Sam & Priya each owe Du 3000.
        var createReq = new CreateEntryRequest("expense", "Gemensam pizza", 9_000, null, SeedIds.Du, null);
        var post = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/entries", createReq, Web);
        var created = await post.Content.ReadFromJsonAsync<EntryDto>(Web);
        var id = created!.Id;
        Assert.False(created.Settled);
        Assert.False(created.Locked);

        // Settle the whole entry → settled + locked.
        var settleRes = await du.PostAsync($"/entries/{id}/settlements", null);
        Assert.Equal(HttpStatusCode.OK, settleRes.StatusCode);
        var settled = await settleRes.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.True(settled!.Settled);
        Assert.True(settled.Locked);
        Assert.Equal("settled", settled.ViewerStatus.Kind);

        // PUT while locked → 409.
        var updateReq = new UpdateEntryRequest("expense", "Ändrad", 9_000, null, SeedIds.Du, null, null);
        var putLocked = await du.PutAsJsonAsync($"/entries/{id}", updateReq, Web);
        Assert.Equal(HttpStatusCode.Conflict, putLocked.StatusCode);
        Assert.Contains("låst", await DetailAsync(putLocked));

        // DELETE while locked → 409.
        var delLocked = await du.DeleteAsync($"/entries/{id}");
        Assert.Equal(HttpStatusCode.Conflict, delLocked.StatusCode);

        // Reopen → not settled, not locked.
        var reopen = await du.DeleteAsync($"/entries/{id}/settlements");
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var reopened = await reopen.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.False(reopened!.Settled);
        Assert.False(reopened.Locked);

        // PUT now allowed.
        var putOk = await du.PutAsJsonAsync($"/entries/{id}", updateReq, Web);
        Assert.Equal(HttpStatusCode.OK, putOk.StatusCode);
        var updated = await putOk.Content.ReadFromJsonAsync<EntryDto>(Web);
        Assert.Equal("Ändrad", updated!.Title);

        // DELETE now allowed.
        var delOk = await du.DeleteAsync($"/entries/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delOk.StatusCode);

        var gone = await du.GetAsync($"/entries/{id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
