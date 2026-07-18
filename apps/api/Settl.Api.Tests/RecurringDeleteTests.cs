using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// WebApplicationFactory integration tests for DELETE /recurring/{id} — the delete-if-clean,
/// else-deactivate rule from the ledger-editing spec. A template with zero posted entries hard-deletes (204);
/// one with posted history is refused (409) so the real debts its cycles created are preserved.
/// </summary>
public class RecurringDeleteTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Delete_clean_template_hard_deletes_and_removes_its_shares()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // A brand-new template has posted nothing yet, so it is "clean".
        var create = await du.PostAsJsonAsync($"/households/{SeedIds.Lonnvagen}/recurring", new
        {
            title = "Elräkning",
            amountMinor = 60_000L,
            cadence = "monthly",
            nextPostDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),
            paidByMemberId = SeedIds.Sam,
            split = new { mode = "equal", values = (object?)null }
        }, Json);
        create.EnsureSuccessStatusCode();
        var newId = (await JsonDocument.ParseAsync(await create.Content.ReadAsStreamAsync()))
            .RootElement.GetProperty("id").GetGuid();

        var del = await du.DeleteAsync($"/recurring/{newId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // Gone from the read model and from the DB (shares cascaded).
        Assert.Equal(HttpStatusCode.NotFound, (await du.GetAsync($"/recurring/{newId}")).StatusCode);
        var (templates, shares) = await factory.WithDb(async db => (
            await db.RecurringTemplates.CountAsync(t => t.Id == newId),
            await db.RecurringShares.CountAsync(s => s.RecurringTemplateId == newId)));
        Assert.Equal(0, templates);
        Assert.Equal(0, shares);
    }

    [Fact]
    public async Task Delete_template_with_posted_history_is_refused_with_409()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Hyra (Rent) has one posted period in the seed ("Hyra — juli") → cannot be deleted.
        var del = await du.DeleteAsync($"/recurring/{SeedIds.Rent}");
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);

        // The template — and the posted entry referencing it — are untouched.
        Assert.Equal(HttpStatusCode.OK, (await du.GetAsync($"/recurring/{SeedIds.Rent}")).StatusCode);
        var stillThere = await factory.WithDb(db =>
            db.Entries.AnyAsync(e => e.RecurringTemplateId == SeedIds.Rent));
        Assert.True(stillThere);
    }

    [Fact]
    public async Task Delete_missing_template_returns_404()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var del = await du.DeleteAsync($"/recurring/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, del.StatusCode);
    }
}
