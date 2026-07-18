using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Integration tests for household ownership + archival
/// (docs/specs/household-ownership.md). Canonical seed: Lönnvägen 3 (owner Du; members
/// Du, Sam, Priya) and Familjen (owner Du; members Du, Mamma, Pappa).
/// </summary>
public class HouseholdOwnershipTests
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

    // ---------- Owner fields ----------

    [Fact]
    public async Task GetHouseholds_carries_owner_fields_and_isOwner_is_viewer_relative()
    {
        using var factory = await SeededAsync();

        var duList = await factory.ClientAs(SeedIds.Du)
            .GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        var duLonn = duList!.Single(h => h.Id == SeedIds.Lonnvagen);
        Assert.Equal(SeedIds.Du, duLonn.OwnerMemberId);
        Assert.True(duLonn.IsOwner);
        Assert.Null(duLonn.ArchivedAt);

        // Sam sees the same owner but is NOT the owner.
        var samList = await factory.ClientAs(SeedIds.Sam)
            .GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        var samLonn = samList!.Single(h => h.Id == SeedIds.Lonnvagen);
        Assert.Equal(SeedIds.Du, samLonn.OwnerMemberId);
        Assert.False(samLonn.IsOwner);
    }

    [Fact]
    public async Task CreateHousehold_makes_creator_the_owner()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var post = await du.PostAsJsonAsync("/households", new CreateHouseholdRequest("Sommarstugan", null), Web);
        var created = await post.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        Assert.Equal(SeedIds.Du, created!.OwnerMemberId);
        Assert.True(created.IsOwner);
        Assert.Null(created.ArchivedAt);
    }

    // ---------- Transfer ownership ----------

    [Fact]
    public async Task TransferOwnership_owner_can_hand_off_to_a_member()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/transfer-ownership",
            new TransferOwnershipRequest(SeedIds.Sam), Web);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        Assert.Equal(SeedIds.Sam, dto!.OwnerMemberId);
        Assert.False(dto.IsOwner); // Du is no longer the owner

        // Sam now sees ownership.
        var samView = await factory.ClientAs(SeedIds.Sam)
            .GetFromJsonAsync<HouseholdDto>($"/households/{SeedIds.Lonnvagen}", Web);
        Assert.True(samView!.IsOwner);
    }

    [Fact]
    public async Task TransferOwnership_by_nonOwner_is_403()
    {
        using var factory = await SeededAsync();
        var sam = factory.ClientAs(SeedIds.Sam);

        var res = await sam.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/transfer-ownership",
            new TransferOwnershipRequest(SeedIds.Priya), Web);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_to_self_is_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/transfer-ownership",
            new TransferOwnershipRequest(SeedIds.Du), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TransferOwnership_to_nonMember_is_400()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // Mamma is a member of Familjen, not Lönnvägen.
        var res = await du.PostAsJsonAsync(
            $"/households/{SeedIds.Lonnvagen}/transfer-ownership",
            new TransferOwnershipRequest(SeedIds.Mamma), Web);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- Leave ----------

    [Fact]
    public async Task Leave_nonOwner_removes_membership()
    {
        using var factory = await SeededAsync();
        var sam = factory.ClientAs(SeedIds.Sam);

        var res = await sam.PostAsync($"/households/{SeedIds.Lonnvagen}/leave", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<LeaveResultDto>(Web);
        Assert.False(result!.Archived);

        // Sam no longer sees Lönnvägen; Du no longer lists Sam as a member.
        var samList = await sam.GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.DoesNotContain(samList!, h => h.Id == SeedIds.Lonnvagen);

        var members = await factory.ClientAs(SeedIds.Du)
            .GetFromJsonAsync<List<MemberDto>>($"/households/{SeedIds.Lonnvagen}/members", Web);
        Assert.DoesNotContain(members!, m => m.Id == SeedIds.Sam);
    }

    [Fact]
    public async Task Leave_owner_with_other_members_is_409()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsync($"/households/{SeedIds.Lonnvagen}/leave", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("Överför", await DetailAsync(res));
    }

    [Fact]
    public async Task Leave_soleOwner_archives_and_keeps_membership()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        // A household where Du is the only member.
        var post = await du.PostAsJsonAsync("/households", new CreateHouseholdRequest("Solo", null), Web);
        var solo = await post.Content.ReadFromJsonAsync<HouseholdDto>(Web);

        var res = await du.PostAsync($"/households/{solo!.Id}/leave", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<LeaveResultDto>(Web);
        Assert.True(result!.Archived);

        // Hidden from the normal list but visible with includeArchived, still owned by Du.
        var active = await du.GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.DoesNotContain(active!, h => h.Id == solo.Id);

        var withArchived = await du.GetFromJsonAsync<List<HouseholdListItemDto>>(
            "/households?includeArchived=true", Web);
        var archived = withArchived!.Single(h => h.Id == solo.Id);
        Assert.NotNull(archived.ArchivedAt);
        Assert.True(archived.IsOwner);
    }

    // ---------- Archive / restore ----------

    [Fact]
    public async Task Archive_hides_from_list_and_restore_brings_it_back()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var archiveRes = await du.PostAsync($"/households/{SeedIds.Lonnvagen}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);
        var archived = await archiveRes.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        Assert.NotNull(archived!.ArchivedAt);

        // Hidden for everyone in the normal list.
        var samList = await factory.ClientAs(SeedIds.Sam)
            .GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.DoesNotContain(samList!, h => h.Id == SeedIds.Lonnvagen);

        var duArchived = await du.GetFromJsonAsync<List<HouseholdListItemDto>>(
            "/households?includeArchived=true", Web);
        Assert.Contains(duArchived!, h => h.Id == SeedIds.Lonnvagen && h.ArchivedAt != null);

        // Restore.
        var restoreRes = await du.PostAsync($"/households/{SeedIds.Lonnvagen}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restoreRes.StatusCode);
        var restored = await restoreRes.Content.ReadFromJsonAsync<HouseholdDto>(Web);
        Assert.Null(restored!.ArchivedAt);

        var backInList = await du.GetFromJsonAsync<List<HouseholdListItemDto>>("/households", Web);
        Assert.Contains(backInList!, h => h.Id == SeedIds.Lonnvagen);
    }

    [Fact]
    public async Task Archive_by_nonOwner_is_403()
    {
        using var factory = await SeededAsync();
        var sam = factory.ClientAs(SeedIds.Sam);

        var res = await sam.PostAsync($"/households/{SeedIds.Lonnvagen}/archive", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Archive_twice_is_409()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        await du.PostAsync($"/households/{SeedIds.Lonnvagen}/archive", null);
        var second = await du.PostAsync($"/households/{SeedIds.Lonnvagen}/archive", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Restore_when_not_archived_is_409()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var res = await du.PostAsync($"/households/{SeedIds.Lonnvagen}/restore", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ---------- Removal preview ----------

    [Fact]
    public async Task RemovalPreview_for_owner_reports_guards_and_debts()
    {
        using var factory = await SeededAsync();
        var du = factory.ClientAs(SeedIds.Du);

        var preview = await du.GetFromJsonAsync<RemovalPreviewDto>(
            $"/households/{SeedIds.Lonnvagen}/removal-preview", Web);
        Assert.NotNull(preview);
        Assert.True(preview!.IsOwner);
        Assert.Equal(3, preview.MemberCount);
        Assert.False(preview.SoleMember);
        Assert.True(preview.MustTransferFirst);

        // Du owes both Sam and Priya in the seed → two per-person open-debt rows.
        Assert.Equal(2, preview.ViewerOpenDebts.Count);
        Assert.All(preview.ViewerOpenDebts, p => Assert.Equal("youOwe", p.Relation));
        Assert.True(preview.HouseholdOpenTotalMinor > 0);
    }

    [Fact]
    public async Task RemovalPreview_for_nonMember_is_404()
    {
        using var factory = await SeededAsync();
        // Mamma is not a member of Lönnvägen.
        var mamma = factory.ClientAs(SeedIds.Mamma);

        var res = await mamma.GetAsync($"/households/{SeedIds.Lonnvagen}/removal-preview");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
