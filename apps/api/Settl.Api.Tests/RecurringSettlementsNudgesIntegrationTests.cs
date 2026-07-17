using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for the Recurring, Settlements and Nudges endpoint
/// groups, driven against the canonical seed (Lönnvägen 3: Du/Sam/Priya). Expected values are
/// computed from the seed fixture (DbInitializer) and the pure calculators, not guessed.
///
/// Seed recap (Lönnvägen), amounts in öre:
///   Templates (all active): Hyra 2 400 000 monthly Amount {Du 900k, Sam 800k, Priya 700k}
///     next=+3d; Städhjälp 120 000 biweekly Equal next=+6d; Spotify 16 900 monthly Equal
///     next=+16d; Internet 44 900 monthly Equal next=+20d.
///   Settled entries (closed in seed): Städmaterial (e6), Hyra — juli (e3).
/// </summary>
public class RecurringSettlementsNudgesIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<(HttpStatusCode Status, JsonDocument Body)> ReadJson(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, JsonDocument.Parse(text));
    }

    private static DateOnly TodayUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static JsonElement FindByGuidProp(JsonElement array, string prop, Guid value)
    {
        foreach (var el in array.EnumerateArray())
            if (el.GetProperty(prop).GetGuid() == value) return el;
        throw new Xunit.Sdk.XunitException($"no element with {prop}={value}");
    }

    // ---------------------------------------------------------------- Recurring

    [Fact]
    public async Task GetRecurring_normalizes_totals_and_lists_all_active_templates()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (status, body) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/recurring"));
        Assert.Equal(HttpStatusCode.OK, status);
        var root = body.RootElement;

        // recTotal = Σ monthly-normalized amounts of active templates:
        //   Hyra 2 400 000×1 + Städhjälp 120 000×2 + Spotify 16 900×1 + Internet 44 900×1
        Assert.Equal(2_701_800L, root.GetProperty("recTotalMinor").GetInt64());
        // recShare = Σ monthly-normalized Du shares:
        //   Hyra 900 000×1 + Städhjälp 40 000×2 + Spotify 5 634×1 + Internet 14 967×1
        Assert.Equal(1_000_601L, root.GetProperty("recShareMinor").GetInt64());

        var templates = root.GetProperty("templates");
        Assert.Equal(4, templates.GetArrayLength());

        // Ordered by daysUntil ascending (Hyra +3 first, Internet +20 last).
        var daysUntils = templates.EnumerateArray().Select(t => t.GetProperty("daysUntil").GetInt32()).ToList();
        var sorted = daysUntils.OrderBy(d => d).ToList();
        Assert.Equal(sorted, daysUntils);

        var hyra = FindByGuidProp(templates, "id", SeedIds.Rent);
        Assert.Equal(2_400_000L, hyra.GetProperty("amountMinor").GetInt64());
        Assert.Equal("monthly", hyra.GetProperty("cadence").GetString());
        Assert.True(hyra.GetProperty("active").GetBoolean());
        Assert.Equal("Du", hyra.GetProperty("payerName").GetString());
        Assert.Equal("amount", hyra.GetProperty("splitMode").GetString());
        Assert.Equal(900_000L, hyra.GetProperty("yourShareMinor").GetInt64());
        Assert.Equal(2_400_000L, hyra.GetProperty("monthlyNormalizedMinor").GetInt64());

        // daysUntil / cycleProgress recomputed from the returned nextPostDate to avoid drift.
        var today = TodayUtc();
        var next = hyra.GetProperty("nextPostDate").GetDateTime();
        var nextDate = DateOnly.FromDateTime(next);
        var expectedDays = nextDate.DayNumber - today.DayNumber;
        Assert.Equal(expectedDays, hyra.GetProperty("daysUntil").GetInt32());
        var expectedProgress = Math.Clamp(1d - (double)expectedDays / 30d, 0.04d, 1.0d);
        Assert.Equal(expectedProgress, hyra.GetProperty("cycleProgress").GetDouble(), 5);

        // All three members contribute to the rent (each share > 0).
        var contributing = hyra.GetProperty("contributingMemberIds").EnumerateArray().Select(e => e.GetGuid()).ToHashSet();
        Assert.Equal(new HashSet<Guid> { SeedIds.Du, SeedIds.Sam, SeedIds.Priya }, contributing);
    }

    [Fact]
    public async Task GetRecurringDetail_returns_frozen_shares_and_posted_entries()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (status, body) = await ReadJson(await du.GetAsync($"/recurring/{SeedIds.Rent}"));
        Assert.Equal(HttpStatusCode.OK, status);
        var root = body.RootElement;

        Assert.Equal(900_000L, root.GetProperty("template").GetProperty("yourShareMinor").GetInt64());

        var shares = root.GetProperty("shares");
        Assert.Equal(3, shares.GetArrayLength());
        var duShare = FindByGuidProp(shares, "memberId", SeedIds.Du);
        Assert.Equal(900_000L, duShare.GetProperty("shareMinor").GetInt64());
        Assert.True(duShare.GetProperty("isPayer").GetBoolean());
        Assert.Equal(800_000L, FindByGuidProp(shares, "memberId", SeedIds.Sam).GetProperty("shareMinor").GetInt64());
        Assert.False(FindByGuidProp(shares, "memberId", SeedIds.Sam).GetProperty("isPayer").GetBoolean());
        Assert.Equal(700_000L, FindByGuidProp(shares, "memberId", SeedIds.Priya).GetProperty("shareMinor").GetInt64());

        // Seed links one RecurringPost (Hyra — juli) to this template, and it is settled.
        var posted = root.GetProperty("postedEntries");
        Assert.Equal(1, posted.GetArrayLength());
        var post = posted[0];
        Assert.Equal("Hyra — juli", post.GetProperty("title").GetString());
        Assert.Equal(2_400_000L, post.GetProperty("amountMinor").GetInt64());
        Assert.True(post.GetProperty("settled").GetBoolean());
    }

    [Fact]
    public async Task PostRecurring_creates_template_without_posting_immediately()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var req = new
        {
            title = "Elräkning",
            amountMinor = 60_000L,
            cadence = "monthly",
            nextPostDate = TodayUtc().AddDays(10),
            paidByMemberId = SeedIds.Sam,
            split = new { mode = "equal", values = (object?)null }
        };

        var (status, body) = await ReadJson(await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", req, Json));
        Assert.Equal(HttpStatusCode.Created, status);
        var root = body.RootElement;

        var newId = root.GetProperty("id").GetGuid();
        Assert.Equal("Elräkning", root.GetProperty("title").GetString());
        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal("Sam", root.GetProperty("payerName").GetString());
        Assert.Equal(60_000L, root.GetProperty("monthlyNormalizedMinor").GetInt64());
        Assert.Equal(20_000L, root.GetProperty("yourShareMinor").GetInt64()); // equal 60 000/3

        // Contract: creating a template does NOT post an entry immediately.
        var (detailStatus, detail) = await ReadJson(await du.GetAsync($"/recurring/{newId}"));
        Assert.Equal(HttpStatusCode.OK, detailStatus);
        Assert.Equal(0, detail.RootElement.GetProperty("postedEntries").GetArrayLength());

        var posts = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == newId));
        Assert.Equal(0, posts);
    }

    [Fact]
    public async Task PatchRecurring_active_false_pauses_and_true_resumes()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Pause Städhjälp (biweekly 120 000 → contributes 240 000 to recTotal, 80 000 to recShare).
        var (pauseStatus, pauseBody) = await ReadJson(await du.PatchAsJsonAsync(
            $"/recurring/{SeedIds.Cleaning}", new { active = false }, Json));
        Assert.Equal(HttpStatusCode.OK, pauseStatus);
        Assert.False(pauseBody.RootElement.GetProperty("active").GetBoolean());
        Assert.Equal(0d, pauseBody.RootElement.GetProperty("cycleProgress").GetDouble()); // inactive → 0

        var (_, pausedList) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/recurring"));
        // Paused template still LISTED, but excluded from the normalized totals.
        Assert.Equal(4, pausedList.RootElement.GetProperty("templates").GetArrayLength());
        var cleaning = FindByGuidProp(pausedList.RootElement.GetProperty("templates"), "id", SeedIds.Cleaning);
        Assert.False(cleaning.GetProperty("active").GetBoolean());
        Assert.Equal(2_461_800L, pausedList.RootElement.GetProperty("recTotalMinor").GetInt64()); // 2 701 800 − 240 000
        Assert.Equal(920_601L, pausedList.RootElement.GetProperty("recShareMinor").GetInt64());   // 1 000 601 − 80 000

        // Resume.
        var (resumeStatus, resumeBody) = await ReadJson(await du.PatchAsJsonAsync(
            $"/recurring/{SeedIds.Cleaning}", new { active = true }, Json));
        Assert.Equal(HttpStatusCode.OK, resumeStatus);
        Assert.True(resumeBody.RootElement.GetProperty("active").GetBoolean());

        var (_, resumedList) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/recurring"));
        Assert.Equal(2_701_800L, resumedList.RootElement.GetProperty("recTotalMinor").GetInt64());
        Assert.Equal(1_000_601L, resumedList.RootElement.GetProperty("recShareMinor").GetInt64());
    }

    [Fact]
    public async Task PatchRecurring_split_rewrites_template_shares()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Internet is 44 900, currently Equal. Switch to Amount {Du 20 000, Sam 15 000, Priya 9 900} (=44 900).
        var req = new
        {
            split = new
            {
                mode = "amount",
                values = new Dictionary<string, decimal>
                {
                    [SeedIds.Du.ToString()] = 20_000m,
                    [SeedIds.Sam.ToString()] = 15_000m,
                    [SeedIds.Priya.ToString()] = 9_900m
                }
            }
        };

        var (status, body) = await ReadJson(await du.PatchAsJsonAsync($"/recurring/{SeedIds.Internet}", req, Json));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("amount", body.RootElement.GetProperty("splitMode").GetString());
        Assert.Equal(20_000L, body.RootElement.GetProperty("yourShareMinor").GetInt64());

        var (_, detail) = await ReadJson(await du.GetAsync($"/recurring/{SeedIds.Internet}"));
        var shares = detail.RootElement.GetProperty("shares");
        Assert.Equal(20_000L, FindByGuidProp(shares, "memberId", SeedIds.Du).GetProperty("shareMinor").GetInt64());
        Assert.Equal(15_000L, FindByGuidProp(shares, "memberId", SeedIds.Sam).GetProperty("shareMinor").GetInt64());
        Assert.Equal(9_900L, FindByGuidProp(shares, "memberId", SeedIds.Priya).GetProperty("shareMinor").GetInt64());
    }

    // ------------------------------------------------------------- Settlements

    [Fact]
    public async Task SettlePreview_returns_net_label_and_signed_entries()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (status, body) = await ReadJson(await du.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settle-preview?person={SeedIds.Sam}"));
        Assert.Equal(HttpStatusCode.OK, status);
        var root = body.RootElement;

        // NetWith(Du, Sam): soffa −96 000, matinköp −28 800, internet-post +14 967,
        //   taxi IOU +12 000, spotify-post −5 634  →  −103 467 (Du owes Sam).
        Assert.Equal(-103_467L, root.GetProperty("netMinor").GetInt64());
        Assert.Equal("youOwe", root.GetProperty("netLabel").GetString());
        Assert.Equal("Sam", root.GetProperty("memberName").GetString());

        var entries = root.GetProperty("entries");
        Assert.Equal(5, entries.GetArrayLength());

        // Signed contributing entries: >0 means Sam owes me, <0 means I owe Sam. Sum equals net.
        var sum = entries.EnumerateArray().Sum(e => e.GetProperty("signedAmountMinor").GetInt64());
        Assert.Equal(-103_467L, sum);

        var soffa = entries.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Begagnad soffa");
        Assert.Equal(-96_000L, soffa.GetProperty("signedAmountMinor").GetInt64()); // Du owes Sam
        var taxi = entries.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Taxi hem");
        Assert.Equal(12_000L, taxi.GetProperty("signedAmountMinor").GetInt64());   // Sam owes Du

        // Entries sorted by date descending.
        var dates = entries.EnumerateArray().Select(e => e.GetProperty("date").GetDateTime()).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    [Fact]
    public async Task PostSettlement_closes_all_pair_debts_but_leaves_third_party_open()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (createStatus, createBody) = await ReadJson(await du.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements",
            new { personMemberId = SeedIds.Sam }, Json));
        Assert.Equal(HttpStatusCode.Created, createStatus);
        Assert.NotEqual(Guid.Empty, createBody.RootElement.GetProperty("settlementId").GetGuid());

        // Summary: the Du↔Sam pair is now square; the Du↔Priya pair is untouched.
        var (summaryStatus, summary) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/summary"));
        Assert.Equal(HttpStatusCode.OK, summaryStatus);
        var people = summary.RootElement.GetProperty("people");
        var sam = FindByGuidProp(people, "memberId", SeedIds.Sam);
        Assert.Equal(0L, sam.GetProperty("netMinor").GetInt64());
        Assert.Equal("square", sam.GetProperty("relation").GetString());
        var priya = FindByGuidProp(people, "memberId", SeedIds.Priya);
        Assert.Equal(-23_034L, priya.GetProperty("netMinor").GetInt64()); // Du still owes Priya
        Assert.Equal("youOwe", priya.GetProperty("relation").GetString());

        // Preview against Sam is now empty / square.
        var (_, preview) = await ReadJson(await du.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settle-preview?person={SeedIds.Sam}"));
        Assert.Equal(0L, preview.RootElement.GetProperty("netMinor").GetInt64());
        Assert.Equal(0, preview.RootElement.GetProperty("entries").GetArrayLength());

        // Per-pair semantics on the 3-way soffa (Du/Sam/Priya, paid by Sam): closing Du↔Sam
        // leaves Priya's debt to Sam open, so the entry is only partially settled and locked.
        var (_, entries) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/entries"));
        var soffa = entries.RootElement.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Begagnad soffa");
        Assert.False(soffa.GetProperty("settled").GetBoolean());
        Assert.True(soffa.GetProperty("locked").GetBoolean());
        Assert.Equal("partiallySettled", soffa.GetProperty("viewerStatus").GetProperty("kind").GetString());

        // The pure two-party IOU (Taxi, Sam→Du) is fully settled by the same settlement.
        var taxi = entries.RootElement.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Taxi hem");
        Assert.True(taxi.GetProperty("settled").GetBoolean());
    }

    [Fact]
    public async Task GetSettlementHistory_returns_pair_events_newest_first()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (status, body) = await ReadJson(await du.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements?person={SeedIds.Sam}"));
        Assert.Equal(HttpStatusCode.OK, status);
        var arr = body.RootElement;

        // The seed records exactly one settlement (Du-initiated), closing e6 Städmaterial
        // (Sam→Du 8 300) and e3 Hyra — juli (Sam→Du 800 000) for the Du↔Sam pair.
        Assert.Equal(1, arr.GetArrayLength());
        var item = arr[0];

        Assert.Equal(SeedIds.Du, item.GetProperty("initiatedByMemberId").GetGuid());
        Assert.Equal(808_300L, item.GetProperty("netClearedMinor").GetInt64()); // Sam owed Du
        Assert.Equal(2, item.GetProperty("closedEntryCount").GetInt32());
        Assert.True(item.TryGetProperty("settledAt", out var settledAt));
        Assert.NotEqual(default, settledAt.GetDateTimeOffset());

        var entries = item.GetProperty("entries");
        Assert.Equal(2, entries.GetArrayLength());

        // Newest-first by entry date: Städmaterial (−10d) before Hyra — juli (−27d).
        var titles = entries.EnumerateArray().Select(e => e.GetProperty("title").GetString()).ToList();
        Assert.Equal(new[] { "Städmaterial", "Hyra — juli" }, titles);

        var stad = entries.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Städmaterial");
        Assert.Equal(8_300L, stad.GetProperty("amountMinor").GetInt64());
        var hyra = entries.EnumerateArray().First(e => e.GetProperty("title").GetString() == "Hyra — juli");
        Assert.Equal(800_000L, hyra.GetProperty("amountMinor").GetInt64());
    }

    [Fact]
    public async Task GetSettlementHistory_empty_for_pair_that_never_settled()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var sam = factory.ClientAs(SeedIds.Sam);

        // The seed settlement only credits Du, so no closure touches the Sam↔Priya pair.
        var (status, body) = await ReadJson(await sam.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements?person={SeedIds.Priya}"));
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(0, body.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task GetSettlementHistory_reflects_a_new_settlement_after_settling()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Before: Du↔Priya has the one seed settlement.
        var (_, before) = await ReadJson(await du.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements?person={SeedIds.Priya}"));
        Assert.Equal(1, before.RootElement.GetArrayLength());

        // Settle the rest of the Du↔Priya pair → a second, Du-initiated event.
        var (createStatus, _) = await ReadJson(await du.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements",
            new { personMemberId = SeedIds.Priya }, Json));
        Assert.Equal(HttpStatusCode.Created, createStatus);

        var (_, after) = await ReadJson(await du.GetAsync(
            $"/households/{SeedIds.Lonnvagen}/settlements?person={SeedIds.Priya}"));
        Assert.Equal(2, after.RootElement.GetArrayLength());
        // Newest-first: the just-created settlement leads.
        var dates = after.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("settledAt").GetDateTimeOffset()).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
    }

    // ------------------------------------------------------------------ Nudges

    [Fact]
    public async Task GetNudges_canonical_seed_surfaces_all_three_kinds_in_order()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (status, body) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges"));
        Assert.Equal(HttpStatusCode.OK, status);
        var arr = body.RootElement;

        var kinds = arr.EnumerateArray().Select(n => n.GetProperty("kind").GetString()).ToList();
        // Exactly one of each kind for Du: Hyra due (+3d), soffa (≥1500 kr, recent), Sam balance (|net|≥750).
        Assert.Equal(1, kinds.Count(k => k == "recurringDue"));
        Assert.Equal(1, kinds.Count(k => k == "bigExpense"));
        Assert.Equal(1, kinds.Count(k => k == "balance"));

        // Fixed emission order: recurringDue → bigExpense → balance.
        Assert.True(kinds.IndexOf("recurringDue") < kinds.IndexOf("bigExpense"));
        Assert.True(kinds.IndexOf("bigExpense") < kinds.IndexOf("balance"));

        var due = arr.EnumerateArray().First(n => n.GetProperty("kind").GetString() == "recurringDue");
        Assert.Contains("Hyra", due.GetProperty("title").GetString());
        var dueAction = due.GetProperty("actions")[0];
        Assert.Equal("viewRecurring", dueAction.GetProperty("kind").GetString());
        Assert.Equal(SeedIds.Rent, dueAction.GetProperty("targetId").GetGuid());

        var big = arr.EnumerateArray().First(n => n.GetProperty("kind").GetString() == "bigExpense");
        Assert.Contains("Begagnad soffa", big.GetProperty("title").GetString());
        var bigActions = big.GetProperty("actions");
        Assert.Contains(bigActions.EnumerateArray(), a => a.GetProperty("kind").GetString() == "viewEntry");
        // Payer is Sam (not me) → a settle action targeting Sam is offered.
        var settleAction = bigActions.EnumerateArray().First(a => a.GetProperty("kind").GetString() == "settle");
        Assert.Equal(SeedIds.Sam, settleAction.GetProperty("targetId").GetGuid());

        var balance = arr.EnumerateArray().First(n => n.GetProperty("kind").GetString() == "balance");
        var balAction = balance.GetProperty("actions")[0];
        Assert.Equal("settle", balAction.GetProperty("kind").GetString());
        Assert.Equal(SeedIds.Sam, balAction.GetProperty("targetId").GetGuid());
    }

    [Fact]
    public async Task GetNudges_tone_changes_copy_only()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var (_, direct) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges?tone=direct"));
        var (_, gentle) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges?tone=gentle"));

        string Title(JsonDocument d, string kind) =>
            d.RootElement.EnumerateArray().First(n => n.GetProperty("kind").GetString() == kind)
                .GetProperty("title").GetString()!;

        // Same set of nudges either way.
        Assert.Equal(direct.RootElement.GetArrayLength(), gentle.RootElement.GetArrayLength());

        // Balance copy: direct is blunt ("Du är skyldig Sam ..."), gentle is soft.
        Assert.NotEqual(Title(direct, "balance"), Title(gentle, "balance"));
        Assert.Equal("Er nota med Sam växer", Title(gentle, "balance"));
        Assert.Contains("skyldig", Title(direct, "balance"));

        // Recurring-due copy: direct says "dras", gentle says "bokförs".
        Assert.Contains("dras", Title(direct, "recurringDue"));
        Assert.Contains("bokförs", Title(gentle, "recurringDue"));
    }

    [Fact]
    public async Task GetNudges_differ_by_acting_member()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        // Du sees a settle action on the soffa (payer Sam ≠ Du) and a balance nudge with Sam.
        var du = factory.ClientAs(SeedIds.Du);
        var (_, duNudges) = await ReadJson(await du.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges"));
        var duBig = duNudges.RootElement.EnumerateArray().First(n => n.GetProperty("kind").GetString() == "bigExpense");
        Assert.Contains(duBig.GetProperty("actions").EnumerateArray(), a => a.GetProperty("kind").GetString() == "settle");
        Assert.Contains(duNudges.RootElement.EnumerateArray(), n => n.GetProperty("kind").GetString() == "balance");

        // Sam is the payer of the soffa → no settle action on it (nothing to settle with self).
        var sam = factory.ClientAs(SeedIds.Sam);
        var (_, samNudges) = await ReadJson(await sam.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges"));
        var samBig = samNudges.RootElement.EnumerateArray().First(n => n.GetProperty("kind").GetString() == "bigExpense");
        Assert.DoesNotContain(samBig.GetProperty("actions").EnumerateArray(), a => a.GetProperty("kind").GetString() == "settle");

        // Priya's balances with both others are under 750 kr → she gets no balance nudge.
        var priya = factory.ClientAs(SeedIds.Priya);
        var (_, priyaNudges) = await ReadJson(await priya.GetAsync($"/households/{SeedIds.Lonnvagen}/nudges"));
        Assert.DoesNotContain(priyaNudges.RootElement.EnumerateArray(), n => n.GetProperty("kind").GetString() == "balance");
    }
}
