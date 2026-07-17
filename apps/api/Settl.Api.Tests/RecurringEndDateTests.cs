using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Endpoint coverage for recurring end dates (recurring-end-date spec): "efter N gånger" resolves
/// to a stored inclusive date, "datum" is validated against the next post, "never" clears it, and
/// the derived <c>ended</c> flag surfaces on reads. The count→date math itself is unit-tested in
/// <see cref="RecurrenceTests"/>; here we assert the wiring through POST/PATCH/GET.
/// </summary>
public class RecurringEndDateTests
{
    private static object EqualSplit() => new { mode = "equal", values = (object?)null };

    private static async Task<(SettlApiFactory f, HttpClient client)> Seeded()
    {
        var f = new SettlApiFactory();
        await f.SeedCanonicalAsync();
        return (f, f.ClientAs(SeedIds.Du));
    }

    private static async Task<JsonElement> GetTemplate(HttpClient client, Guid id)
    {
        var doc = JsonDocument.Parse(await client.GetStringAsync($"/recurring/{id}"));
        return doc.RootElement.GetProperty("template");
    }

    [Fact]
    public async Task Create_with_count_resolves_to_the_nth_post_date_and_is_not_ended()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            // Monthly, first post 2026-08-01, "efter 12 gånger" → 12th post 2027-07-01.
            var resp = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Gymkort",
                amountMinor = 30000L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                endMode = "count",
                endAfterCount = 12,
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

            using var created = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var t = created.RootElement;
            Assert.Equal("2027-07-01", t.GetProperty("endDate").GetString());
            Assert.False(t.GetProperty("ended").GetBoolean());
        }
    }

    [Fact]
    public async Task Create_with_date_stores_the_date_verbatim()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            var resp = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Hyra sommarstuga",
                amountMinor = 500000L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                endMode = "date",
                endDate = "2026-10-15",
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            using var created = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.Equal("2026-10-15", created.RootElement.GetProperty("endDate").GetString());
        }
    }

    [Fact]
    public async Task Create_with_end_date_before_next_post_is_rejected()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            var resp = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Fel",
                amountMinor = 10000L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                endMode = "date",
                endDate = "2026-07-01", // before the first post
            });
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        }
    }

    [Fact]
    public async Task Create_with_never_or_omitted_end_mode_has_no_end_date()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            var resp = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Löpande",
                amountMinor = 10000L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                // endMode omitted → treated as "never"
            });
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            using var created = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null, created.RootElement.GetProperty("endDate").ValueKind);
            Assert.False(created.RootElement.GetProperty("ended").GetBoolean());
        }
    }

    [Fact]
    public async Task Patch_never_clears_a_previously_set_end_date()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            var create = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Kurs",
                amountMinor = 20000L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                endMode = "count",
                endAfterCount = 3,
            });
            using var createdDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var id = Guid.Parse(createdDoc.RootElement.GetProperty("id").GetString()!);
            Assert.Equal("2026-10-01", createdDoc.RootElement.GetProperty("endDate").GetString());

            // Clear it.
            var patch = await client.PatchAsync($"/recurring/{id}",
                JsonContent.Create(new { endMode = "never" }));
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

            var t = await GetTemplate(client, id);
            Assert.Equal(JsonValueKind.Null, t.GetProperty("endDate").ValueKind);
        }
    }

    [Fact]
    public async Task Patch_without_end_mode_leaves_the_end_date_untouched()
    {
        var (f, client) = await Seeded();
        using (f)
        {
            var create = await client.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
            {
                title = "Streaming",
                amountMinor = 9900L,
                cadence = "monthly",
                nextPostDate = "2026-08-01",
                paidByMemberId = SeedIds.Du,
                split = EqualSplit(),
                endMode = "date",
                endDate = "2026-12-01",
            });
            using var createdDoc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var id = Guid.Parse(createdDoc.RootElement.GetProperty("id").GetString()!);

            // Change only the amount — end date must survive.
            var patch = await client.PatchAsync($"/recurring/{id}",
                JsonContent.Create(new { title = "Streaming+" }));
            Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

            var t = await GetTemplate(client, id);
            Assert.Equal("2026-12-01", t.GetProperty("endDate").GetString());
        }
    }
}
