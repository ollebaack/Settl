using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Features;

/// <summary>
/// The one place that turns a household's stored ledger into a member's nudges, shared by the
/// read path (GET /households/{id}/nudges) and the delivery path (the daily digest). Both must
/// see the identical set — computing them twice would risk divergence — so the loading and
/// input-building live here, and callers differ only in what they do with the result: the
/// endpoint drops the identity keys, the digest de-duplicates against them.
/// </summary>
public static class NudgeComputation
{
    /// <summary>
    /// A member's emittable nudges for one household, in fixed emission order. Returns null when
    /// the household does not exist (the endpoint turns that into a 404; the digest never asks for
    /// a household the member isn't in). <paramref name="tone"/> selects copy only, never which
    /// nudges fire.
    /// </summary>
    public static async Task<IReadOnlyList<NudgeCalculator.EmittableNudge>?> ForMember(
        SettlDbContext db, Guid householdId, Guid memberId, string tone, DateOnly today, CancellationToken ct)
    {
        var data = await Loaders.LoadHousehold(db, householdId, ct);
        if (data is null) return null;

        var entries = await Loaders.LoadEntries(db, householdId, ct);
        var closures = await Loaders.LoadClosures(db, householdId, ct);
        var closureEvents = await Loaders.LoadClosureEvents(db, householdId, ct);
        var templates = await db.RecurringTemplates
            .Where(t => t.HouseholdId == householdId && t.Active)
            .Include(t => t.Shares)
            .ToListAsync(ct);

        var recurrings = templates
            .OrderBy(t => SwedishDates.DaysUntil(t.NextPostDate, today))
            .Select(t =>
            {
                var myShare = Mapping.TemplateShares(t, data.OrderedMemberIds).Where(s => s.MemberId == memberId).Sum(s => s.ShareMinor);
                return new NudgeCalculator.RecurringDueInput(t.Id, t.Title, t.NextPostDate, myShare);
            })
            .ToList();

        var expenses = entries
            .Where(e => e.PaidByMemberId is not null)
            .OrderByDescending(e => e.Date)
            .Select(e =>
            {
                var payerId = e.PaidByMemberId!.Value;
                var myShare = e.Shares.Where(s => s.MemberId == memberId).Sum(s => s.ShareMinor);
                return new NudgeCalculator.BigExpenseInput(
                    e.Id, e.Title, e.AmountMinor, e.Date, payerId,
                    Mapping.Name(data.MembersById, payerId), myShare,
                    payerId == memberId, BalanceCalculator.IsSettled(e, closures));
            })
            .ToList();

        var balances = data.OrderedMemberIds
            .Where(m => m != memberId)
            .Select(x => new NudgeCalculator.BalanceInput(
                x,
                Mapping.Name(data.MembersById, x),
                BalanceCalculator.NetWith(memberId, x, entries, closures),
                BalanceCalculator.MostRecentThresholdCrossing(
                    memberId, x, entries, closureEvents, NudgeCalculator.BalanceThresholdMinor)))
            .ToList();

        return NudgeCalculator.BuildEmittable(tone, today, recurrings, expenses, balances);
    }
}
