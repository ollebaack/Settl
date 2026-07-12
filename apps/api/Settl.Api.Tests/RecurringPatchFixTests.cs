using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Regression tests for the Phase-6 recurring-template fixes:
/// (1) PATCH must not leave an Amount-mode template whose frozen shares no longer reconcile
///     with a changed amount (that would 500 reads and wedge the background poster);
/// (2) resuming a paused template must skip the paused gap instead of back-posting it.
/// </summary>
public class RecurringPatchFixTests
{
    private static async Task<(SettlApiFactory f, Guid hyraId)> SeededWithHyra()
    {
        var f = new SettlApiFactory();
        await f.SeedCanonicalAsync();
        var client = f.ClientAs(SeedIds.Du);
        using var doc = JsonDocument.Parse(
            await client.GetStringAsync($"/households/{SeedIds.Lonnvagen}/recurring"));
        var hyra = doc.RootElement.GetProperty("templates").EnumerateArray()
            .First(t => t.GetProperty("title").GetString() == "Hyra");
        return (f, Guid.Parse(hyra.GetProperty("id").GetString()!));
    }

    [Fact]
    public async Task Patch_amount_only_on_amount_mode_template_is_rejected_not_wedged()
    {
        var (f, hyraId) = await SeededWithHyra();
        using (f)
        {
            var client = f.ClientAs(SeedIds.Du);

            // Change the amount WITHOUT re-supplying the kr split → must be rejected (the stored
            // 9000/8000/7000 kr shares can no longer sum to the new total).
            var resp = await client.PatchAsync($"/recurring/{hyraId}",
                JsonContent.Create(new { amountMinor = 2_500_000L }));
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

            // Crucially: the template is NOT wedged — reads still succeed and the amount is unchanged.
            var detail = await client.GetAsync($"/recurring/{hyraId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var doc = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            Assert.Equal(2_400_000L, doc.RootElement.GetProperty("template").GetProperty("amountMinor").GetInt64());
        }
    }

    [Fact]
    public async Task Patch_amount_with_matching_split_on_amount_mode_succeeds()
    {
        var (f, hyraId) = await SeededWithHyra();
        using (f)
        {
            var client = f.ClientAs(SeedIds.Du);
            var body = new
            {
                amountMinor = 3_000_000L,
                split = new
                {
                    mode = "amount",
                    values = new Dictionary<string, decimal>
                    {
                        [SeedIds.Du.ToString()] = 1_200_000m,
                        [SeedIds.Sam.ToString()] = 1_000_000m,
                        [SeedIds.Priya.ToString()] = 800_000m,
                    },
                },
            };
            var resp = await client.PatchAsync($"/recurring/{hyraId}", JsonContent.Create(body));
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }
    }

    [Fact]
    public async Task Resume_fast_forwards_next_post_date_past_the_paused_gap()
    {
        var (f, hyraId) = await SeededWithHyra();
        using (f)
        {
            var client = f.ClientAs(SeedIds.Du);
            var past = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-3);

            // Pause and shove the next post date well into the past (simulating a long pause).
            await client.PatchAsync($"/recurring/{hyraId}",
                JsonContent.Create(new { active = false, nextPostDate = past.ToString("yyyy-MM-dd") }));

            // Resume — the paused gap must be skipped, not queued for a back-post burst.
            var resume = await client.PatchAsync($"/recurring/{hyraId}", JsonContent.Create(new { active = true }));
            Assert.Equal(HttpStatusCode.OK, resume.StatusCode);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var next = await f.WithDb(async db =>
                (await db.RecurringTemplates.FindAsync(hyraId))!.NextPostDate);
            Assert.True(next.DayNumber >= today.DayNumber, $"expected next >= today, got {next}");
        }
    }

    [Theory]
    [InlineData(-90, Cadence.Monthly)]
    [InlineData(-30, Cadence.Weekly)]
    [InlineData(-100, Cadence.Biweekly)]
    public void FastForwardToOnOrAfter_lands_on_or_after_today_on_a_cycle_boundary(int startOffset, Cadence cadence)
    {
        var today = new DateOnly(2026, 7, 12);
        var start = today.AddDays(startOffset);
        var result = RecurrenceCalculator.FastForwardToOnOrAfter(start, cadence, today);

        Assert.True(result.DayNumber >= today.DayNumber);
        // The previous cycle must be strictly before today (i.e. we didn't overshoot).
        var prev = cadence switch
        {
            Cadence.Monthly => result.AddMonths(-1),
            Cadence.Biweekly => result.AddDays(-14),
            _ => result.AddDays(-7),
        };
        Assert.True(prev.DayNumber < today.DayNumber);
    }

    [Fact]
    public void FastForward_is_noop_when_already_future()
    {
        var today = new DateOnly(2026, 7, 12);
        var future = today.AddDays(10);
        Assert.Equal(future, RecurrenceCalculator.FastForwardToOnOrAfter(future, Cadence.Monthly, today));
    }
}
