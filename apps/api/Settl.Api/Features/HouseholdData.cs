using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Features;

/// <summary>A household plus its members in membership order — the common read context.</summary>
public sealed record HouseholdData(
    Household Household,
    IReadOnlyList<Guid> OrderedMemberIds,
    IReadOnlyList<Member> OrderedMembers,
    IReadOnlyDictionary<Guid, Member> MembersById);

public static class Loaders
{
    public static async Task<HouseholdData?> LoadHousehold(SettlDbContext db, Guid householdId, CancellationToken ct)
    {
        var hh = await db.Households
            .Include(h => h.Memberships).ThenInclude(m => m.Member)
            .FirstOrDefaultAsync(h => h.Id == householdId, ct);
        if (hh is null) return null;

        var order = MembershipOrder.Order(hh.Memberships);
        var byId = hh.Memberships.ToDictionary(m => m.MemberId, m => m.Member);
        var orderedMembers = order.Select(id => byId[id]).ToList();
        return new HouseholdData(hh, order, orderedMembers, byId);
    }

    public static async Task<List<Entry>> LoadEntries(SettlDbContext db, Guid householdId, CancellationToken ct) =>
        await db.Entries
            .Where(e => e.HouseholdId == householdId)
            .Include(e => e.Shares)
            .ToListAsync(ct);

    public static async Task<ClosureLookup> LoadClosures(SettlDbContext db, Guid householdId, CancellationToken ct) =>
        new(await db.SettlementClosures
            .Where(c => c.Settlement.HouseholdId == householdId)
            .ToListAsync(ct));

    /// <summary>Closures tagged with their settlement's timestamp, for balance-timeline replay
    /// (ADR-0023, <see cref="BalanceCalculator.MostRecentThresholdCrossing"/>).</summary>
    public static async Task<List<PairClosure>> LoadClosureEvents(SettlDbContext db, Guid householdId, CancellationToken ct) =>
        await db.SettlementClosures
            .Where(c => c.Settlement.HouseholdId == householdId)
            .Select(c => new PairClosure(
                c.EntryId, c.DebtorMemberId, c.CreditorMemberId, c.Settlement.SettledAt))
            .ToListAsync(ct);

    public static async Task<Dictionary<Guid, string>> LoadTemplateTitles(SettlDbContext db, Guid householdId, CancellationToken ct) =>
        await db.RecurringTemplates
            .Where(t => t.HouseholdId == householdId)
            .ToDictionaryAsync(t => t.Id, t => t.Title, ct);
}

/// <summary>Contract label vocabularies (they differ by endpoint per §7).</summary>
public static class Labels
{
    /// <summary>Overall net / household list: "owed" | "owe" | "square".</summary>
    public static string Net(long netMinor) => netMinor > 0 ? "owed" : netMinor < 0 ? "owe" : "square";

    /// <summary>Per-person relation and settle-preview: "owesYou" | "youOwe" | "square".</summary>
    public static string Relation(long netMinor) => netMinor > 0 ? "owesYou" : netMinor < 0 ? "youOwe" : "square";
}
