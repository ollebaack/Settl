using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for GET /households/{id}/stats/contributions
/// (docs/specs/household-statistics.md). Uses ad-hoc <see cref="TestScenario"/> households so
/// the month-window assertions don't depend on the canonical seed's fixed dates. Dates are day
/// offsets from UtcNow, so assertions are relational (bucket counts, current-month sums,
/// in-window totals) rather than hard-coded calendar dates.
/// </summary>
public class HouseholdStatsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Inserts a bare RecurringPost entry (no shares needed by the stats endpoint) so the test
    // can prove both entry types are summed. Mirrors a posted recurring cycle's payer + date.
    private static Task AddRecurringPost(
        SettlApiFactory factory, Guid householdId, Guid paidBy, long amountMinor, int offsetDays) =>
        factory.WithDb(async db =>
        {
            db.Entries.Add(new Entry
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId,
                Type = EntryType.RecurringPost,
                Title = "Hyra",
                AmountMinor = amountMinor,
                Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offsetDays),
                CreatedAt = DateTimeOffset.UtcNow,
                PaidByMemberId = paidBy,
                SplitMode = SplitMode.Equal,
            });
            await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Contributions_from_a_household_the_caller_isnt_in_returns_404()
    {
        using var factory = new SettlApiFactory();
        var s = new TestScenario();
        var anna = s.AddMember("Anna");
        s.AddEqualExpense("Mat", 10_000, anna);
        await factory.SeedAsync(s);

        // A real, logged-in user who belongs to a different household.
        var other = new TestScenario("Annat hushåll");
        var outsider = other.AddMember("Otto");
        await factory.SeedAsync(other);

        var client = factory.ClientAs(outsider);
        var res = await client.GetAsync($"/households/{s.HouseholdId}/stats/contributions");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Contributions_default_range_buckets_by_month_and_attributes_payer()
    {
        using var factory = new SettlApiFactory();
        var s = new TestScenario();
        var anna = s.AddMember("Anna");
        var bo = s.AddMember("Bo");
        var cecilia = s.AddMember("Cecilia");          // pays nothing → excluded from series
        s.AddEqualExpense("Mat", 10_000, anna, dateOffset: -1);       // current month
        s.AddEqualExpense("Fika", 5_000, bo, dateOffset: -1);         // current month
        s.AddEqualExpense("Gammalt", 3_000, anna, dateOffset: -40);   // earlier, still in window
        s.AddEqualExpense("Urgammalt", 999_999, anna, dateOffset: -400); // >12 months → out of window
        await factory.SeedAsync(s);
        await AddRecurringPost(factory, s.HouseholdId, bo, 2_000, offsetDays: -1); // current month

        var client = factory.ClientAs(anna);
        var stats = await client.GetFromJsonAsync<ContributionStatsDto>(
            $"/households/{s.HouseholdId}/stats/contributions", Web);

        Assert.NotNull(stats);
        Assert.Equal("SEK", stats!.Currency);

        // Trailing 12 whole months → 12 continuous buckets, chronological.
        Assert.Equal(12, stats.Buckets.Count);
        for (var i = 1; i < stats.Buckets.Count; i++)
            Assert.True(string.CompareOrdinal(stats.Buckets[i - 1].Month, stats.Buckets[i].Month) < 0,
                "buckets must be chronological");

        // Series = only members with contributions in range, in membership order; Cecilia is out.
        Assert.Equal(new[] { anna, bo }, stats.Members.Select(m => m.MemberId).ToArray());
        Assert.DoesNotContain(stats.Members, m => m.MemberId == cecilia);

        // Every bucket is zero-filled for exactly the series members.
        Assert.All(stats.Buckets, b => Assert.Equal(2, b.PerMember.Count));

        // Current month (last bucket): expense sums, and Bo's recurring post is included.
        var current = stats.Buckets[^1];
        Assert.Equal(10_000, current.PerMember.Single(p => p.MemberId == anna).PaidMinor);
        Assert.Equal(7_000, current.PerMember.Single(p => p.MemberId == bo).PaidMinor); // 5000 + 2000

        // Windowing: the >12-month-old 999_999 entry is excluded → Anna's in-window total is 13_000.
        var annaTotal = stats.Buckets.Sum(b => b.PerMember.Single(p => p.MemberId == anna).PaidMinor);
        Assert.Equal(13_000, annaTotal);
    }

    [Fact]
    public async Task Contributions_default_range_clips_to_one_zero_lead_in_before_the_first_entry()
    {
        using var factory = new SettlApiFactory();
        var s = new TestScenario();
        var anna = s.AddMember("Anna");
        // A young household: its first-ever entry is ~2 months back, and there is nothing
        // older than the trailing-12 cap. The default window should clip the empty 12-month
        // runway but keep exactly one zero-filled lead-in month before the first entry, so
        // the series rises from 0 rather than starting mid-air.
        s.AddEqualExpense("Gammalt", 3_000, anna, dateOffset: -40);   // first entry
        s.AddEqualExpense("Mat", 10_000, anna, dateOffset: -1);       // current month
        await factory.SeedAsync(s);

        var client = factory.ClientAs(anna);
        var stats = await client.GetFromJsonAsync<ContributionStatsDto>(
            $"/households/{s.HouseholdId}/stats/contributions", Web);

        Assert.NotNull(stats);

        // Window starts one month before the first entry (≤ 4 buckets for a ~40-day span), not 12.
        Assert.True(stats!.Buckets.Count < 12, "empty leading months must be clipped");
        var firstMonth = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40);
        var leadIn = new DateOnly(firstMonth.Year, firstMonth.Month, 1).AddMonths(-1);
        Assert.Equal($"{leadIn.Year:D4}-{leadIn.Month:D2}", stats.Buckets[0].Month);

        // The lead-in month is zero-filled — nothing was paid before the household started.
        Assert.Equal(0, stats.Buckets[0].PerMember.Single(p => p.MemberId == anna).PaidMinor);
        // And the first entry's own month is the very next bucket.
        Assert.Equal($"{firstMonth.Year:D4}-{firstMonth.Month:D2}", stats.Buckets[1].Month);

        // Nothing is dropped: both of Anna's payments are still summed across the clipped window.
        var annaTotal = stats.Buckets.Sum(b => b.PerMember.Single(p => p.MemberId == anna).PaidMinor);
        Assert.Equal(13_000, annaTotal);
    }

    [Fact]
    public async Task Contributions_custom_range_is_month_inclusive_and_zero_filled()
    {
        using var factory = new SettlApiFactory();
        var s = new TestScenario();
        var anna = s.AddMember("Anna");
        s.AddEqualExpense("Mat", 10_000, anna, dateOffset: -1);
        await factory.SeedAsync(s);

        // A historical window with no activity: to= is inclusive of its month.
        var client = factory.ClientAs(anna);
        var stats = await client.GetFromJsonAsync<ContributionStatsDto>(
            $"/households/{s.HouseholdId}/stats/contributions?from=2020-01-01&to=2020-03-15", Web);

        Assert.NotNull(stats);
        Assert.Equal(new[] { "2020-01", "2020-02", "2020-03" }, stats!.Buckets.Select(b => b.Month).ToArray());
        Assert.Empty(stats.Members);                          // no contributions in range
        Assert.All(stats.Buckets, b => Assert.Empty(b.PerMember)); // no series → no per-member rows
    }
}
