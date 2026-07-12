using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class HouseholdsEndpoints
{
    public static IEndpointRouteBuilder MapHouseholdsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households", async (ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var householdIds = await db.HouseholdMemberships
                .Where(m => m.MemberId == me)
                .Select(m => m.HouseholdId)
                .ToListAsync(ct);

            var result = new List<HouseholdListItemDto>();
            foreach (var hid in householdIds)
            {
                var data = await Loaders.LoadHousehold(db, hid, ct);
                if (data is null) continue;
                var entries = await Loaders.LoadEntries(db, hid, ct);
                var closures = await Loaders.LoadClosures(db, hid, ct);

                var overall = OverallNet(me.Value, data, entries, closures);
                result.Add(new HouseholdListItemDto(
                    data.Household.Id,
                    data.Household.Name,
                    data.Household.Currency,
                    data.OrderedMembers.Select(m => m.Name).ToList(),
                    overall,
                    Labels.Net(overall)));
            }

            return Results.Ok(result);
        }).WithName("GetHouseholds")
            .Produces<List<HouseholdListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households", async (CreateHouseholdRequest req, SettlDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Namn krävs", statusCode: 400);
            if (req.MemberIds is null || req.MemberIds.Length == 0)
                return Results.Problem("Minst en medlem krävs", statusCode: 400);

            var validMembers = await db.Members.Where(m => req.MemberIds.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct);
            if (validMembers.Count != req.MemberIds.Distinct().Count())
                return Results.Problem("Okänd medlem", statusCode: 400);

            var now = DateTimeOffset.UtcNow;
            var household = new Household
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Currency = string.IsNullOrWhiteSpace(req.Currency) ? "SEK" : req.Currency!.Trim(),
                CreatedAt = now
            };
            db.Households.Add(household);

            var i = 0;
            foreach (var mid in req.MemberIds.Distinct())
                db.HouseholdMemberships.Add(new HouseholdMembership
                { HouseholdId = household.Id, MemberId = mid, JoinedAt = now.AddSeconds(i++) });

            await db.SaveChangesAsync(ct);

            var members = await db.Members.Where(m => req.MemberIds.Contains(m.Id)).ToListAsync(ct);
            var dto = new HouseholdDto(household.Id, household.Name, household.Currency,
                members.Select(m => new MemberDto(m.Id, m.Name, m.AvatarColor)).ToList());
            return Results.Created($"/households/{household.Id}", dto);
        }).WithName("CreateHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapGet("/households/{id:guid}", async (Guid id, SettlDbContext db, CancellationToken ct) =>
        {
            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            return Results.Ok(new HouseholdDto(
                data.Household.Id, data.Household.Name, data.Household.Currency,
                data.OrderedMembers.Select(m => new MemberDto(m.Id, m.Name, m.AvatarColor)).ToList()));
        }).WithName("GetHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}/members", async (Guid id, SettlDbContext db, CancellationToken ct) =>
        {
            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
            return Results.Ok(data.OrderedMembers.Select(m => new MemberDto(m.Id, m.Name, m.AvatarColor)));
        }).WithName("GetHouseholdMembers")
            .Produces<List<MemberDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}/summary", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);

            var others = data.OrderedMemberIds.Where(m => m != me).ToList();
            var people = others.Select(x =>
            {
                var net = BalanceCalculator.NetWith(me.Value, x, entries, closures);
                return new PersonBalanceDto(
                    x, Mapping.Name(data.MembersById, x), data.MembersById[x].AvatarColor, net, Labels.Relation(net));
            }).ToList();

            var overall = people.Sum(p => p.NetMinor);
            var openCount = entries.Count(e => BalanceCalculator.OpenDebts(e, closures).Count > 0);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var templates = await db.RecurringTemplates
                .Where(t => t.HouseholdId == id && t.Active)
                .Include(t => t.Shares)
                .ToListAsync(ct);

            var upcoming = templates
                .Select(t => (t, days: SwedishDates.DaysUntil(t.NextPostDate, today)))
                .Where(x => x.days <= 30)
                .OrderBy(x => x.days)
                .Take(4)
                .Select(x =>
                {
                    var shares = Mapping.TemplateShares(x.t, data.OrderedMemberIds);
                    var yourShare = shares.Where(s => s.MemberId == me).Sum(s => s.ShareMinor);
                    return new UpcomingDto(x.t.Id, x.t.Title, x.t.NextPostDate, x.days, yourShare, x.t.AmountMinor);
                })
                .ToList();

            return Results.Ok(new HouseholdSummaryDto(overall, Labels.Net(overall), openCount, people, upcoming));
        }).WithName("GetHouseholdSummary")
            .Produces<HouseholdSummaryDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static long OverallNet(Guid me, HouseholdData data, IReadOnlyList<Entry> entries, ClosureLookup closures) =>
        data.OrderedMemberIds.Where(m => m != me)
            .Sum(x => BalanceCalculator.NetWith(me, x, entries, closures));
}
