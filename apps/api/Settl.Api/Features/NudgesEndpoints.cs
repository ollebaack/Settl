using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class NudgesEndpoints
{
    public static IEndpointRouteBuilder MapNudgesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/nudges", async (
            Guid id, string? tone, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);
            var closureEvents = await Loaders.LoadClosureEvents(db, id, ct);
            var templates = await db.RecurringTemplates
                .Where(t => t.HouseholdId == id && t.Active)
                .Include(t => t.Shares)
                .ToListAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var recurrings = templates
                .OrderBy(t => SwedishDates.DaysUntil(t.NextPostDate, today))
                .Select(t =>
                {
                    var myShare = Mapping.TemplateShares(t, data.OrderedMemberIds).Where(s => s.MemberId == me).Sum(s => s.ShareMinor);
                    return new NudgeCalculator.RecurringDueInput(t.Id, t.Title, t.NextPostDate, myShare);
                })
                .ToList();

            var expenses = entries
                .Where(e => e.PaidByMemberId is not null)
                .OrderByDescending(e => e.Date)
                .Select(e =>
                {
                    var payerId = e.PaidByMemberId!.Value;
                    var myShare = e.Shares.Where(s => s.MemberId == me).Sum(s => s.ShareMinor);
                    return new NudgeCalculator.BigExpenseInput(
                        e.Id, e.Title, e.AmountMinor, e.Date, payerId,
                        Mapping.Name(data.MembersById, payerId), myShare,
                        payerId == me, BalanceCalculator.IsSettled(e, closures));
                })
                .ToList();

            var balances = data.OrderedMemberIds
                .Where(m => m != me)
                .Select(x => new NudgeCalculator.BalanceInput(
                    x,
                    Mapping.Name(data.MembersById, x),
                    BalanceCalculator.NetWith(me.Value, x, entries, closures),
                    BalanceCalculator.MostRecentThresholdCrossing(
                        me.Value, x, entries, closureEvents, NudgeCalculator.BalanceThresholdMinor)))
                .ToList();

            var nudges = NudgeCalculator.Build(tone ?? "direct", today, recurrings, expenses, balances);
            return Results.Ok(nudges);
        }).WithName("GetNudges")
            .Produces<List<NudgeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
