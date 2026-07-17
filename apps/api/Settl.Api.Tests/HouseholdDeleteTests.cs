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
/// Integration tests for hard-deleting empty households (ADR-0022 /
/// docs/specs/household-ownership.md) — a carve-out to ADR-0016's soft-only rule. Owner-only;
/// only when there is no ledger activity; extra members and pending invites don't block and
/// are cascade-removed. Canonical seed: Lönnvägen 3 (owner Du, has entries) and Familjen.
/// </summary>
public class HouseholdDeleteTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static async Task<SettlApiFactory> SeededAsync()
    {
        var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();
        return factory;
    }

    /// <summary>Creates a fresh, empty household owned solely by <paramref name="ownerEmail"/>.</summary>
    private static async Task<Guid> CreateEmptyHouseholdAsync(SettlApiFactory factory, Guid owner)
    {
        var post = await factory.ClientAs(owner)
            .PostAsJsonAsync("/households", new CreateHouseholdRequest("Feluppgjort", null), Web);
        var created = await post.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        return created!.Id;
    }

    [Fact]
    public async Task Delete_empty_household_by_owner_removes_it_everywhere()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await CreateEmptyHouseholdAsync(factory, SeedIds.Du);

        var res = await du.DeleteAsync($"/households/{id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        // Gone from both the active and the include-archived lists.
        var active = await du.GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.DoesNotContain(active!, h => h.Id == id);
        var withArchived = await du.GetFromJsonAsync<List<HouseholdListItemDto>>(
            "/households?includeArchived=true", Web);
        Assert.DoesNotContain(withArchived!, h => h.Id == id);

        // And truly removed from the database.
        var stillExists = await factory.WithDb(db => db.Households.AnyAsync(h => h.Id == id));
        Assert.False(stillExists);
    }

    [Fact]
    public async Task Delete_household_with_entries_is_409_and_keeps_it()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Lönnvägen has seeded entries → not empty.
        var res = await du.DeleteAsync($"/households/{SeedIds.Lonnvagen}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        var stillExists = await factory.WithDb(db => db.Households.AnyAsync(h => h.Id == SeedIds.Lonnvagen));
        Assert.True(stillExists);
    }

    [Fact]
    public async Task Delete_household_with_only_a_recurring_template_is_409()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await CreateEmptyHouseholdAsync(factory, SeedIds.Du);

        // A template but no posted entries still counts as activity (ADR-0022 empty def).
        await factory.WithDb(async db =>
        {
            db.RecurringTemplates.Add(new RecurringTemplate
            {
                Id = Guid.NewGuid(),
                HouseholdId = id,
                Title = "Hyra",
                AmountMinor = 100_00,
                Cadence = Cadence.Monthly,
                NextPostDate = new DateOnly(2026, 8, 1),
                PaidByMemberId = SeedIds.Du,
                SplitMode = SplitMode.Equal,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        });

        var res = await du.DeleteAsync($"/households/{id}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Delete_by_nonOwner_member_is_403()
    {
        using var factory = await SeededAsync();
        var id = await CreateEmptyHouseholdAsync(factory, SeedIds.Du);

        // Add Sam as an ordinary member of the empty household.
        await factory.WithDb(async db =>
        {
            db.HouseholdMemberships.Add(new HouseholdMembership
            { HouseholdId = id, MemberId = SeedIds.Sam, JoinedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        var res = await factory.ClientAs(SeedIds.Sam).DeleteAsync($"/households/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);

        var stillExists = await factory.WithDb(db => db.Households.AnyAsync(h => h.Id == id));
        Assert.True(stillExists);
    }

    [Fact]
    public async Task Delete_by_nonMember_is_404()
    {
        using var factory = await SeededAsync();
        // Mamma is not a member of Lönnvägen — existence must not leak.
        var res = await factory.ClientAs(SeedIds.Mamma).DeleteAsync($"/households/{SeedIds.Lonnvagen}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Delete_empty_household_removes_other_members_and_pending_invites()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var id = await CreateEmptyHouseholdAsync(factory, SeedIds.Du);

        // Owner overrides extra members (ADR-0022): Sam has joined, and there's a pending invite.
        var inviteId = Guid.NewGuid();
        await factory.WithDb(async db =>
        {
            db.HouseholdMemberships.Add(new HouseholdMembership
            { HouseholdId = id, MemberId = SeedIds.Sam, JoinedAt = DateTimeOffset.UtcNow });
            db.Invites.Add(new Invite
            {
                Id = inviteId,
                HouseholdId = id,
                Channel = InviteChannel.Email,
                Email = "granne@example.com",
                TokenHash = "delete-test-" + inviteId,
                InvitedByMemberId = SeedIds.Du,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            });
            await db.SaveChangesAsync();
        });

        var res = await du.DeleteAsync($"/households/{id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        // Sam no longer sees it, and the membership + pending invite are cascade-removed.
        var samList = await factory.ClientAs(SeedIds.Sam)
            .GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.DoesNotContain(samList!, h => h.Id == id);

        await factory.WithDb(async db =>
        {
            Assert.False(await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id));
            Assert.False(await db.Invites.AnyAsync(i => i.Id == inviteId));
        });
    }

    [Fact]
    public async Task RemovalPreview_reports_isEmpty()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);
        var emptyId = await CreateEmptyHouseholdAsync(factory, SeedIds.Du);

        var emptyPreview = await du.GetFromJsonAsync<RemovalPreviewDto>(
            $"/households/{emptyId}/removal-preview", Web);
        Assert.True(emptyPreview!.IsEmpty);

        var lonnPreview = await du.GetFromJsonAsync<RemovalPreviewDto>(
            $"/households/{SeedIds.Lonnvagen}/removal-preview", Web);
        Assert.False(lonnPreview!.IsEmpty);
    }
}
