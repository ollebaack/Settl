using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class SettlementsEndpoints
{
    public static IEndpointRouteBuilder MapSettlementsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/settle-preview", async (
            Guid id, Guid person, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (!data.MembersById.TryGetValue(person, out var other))
                return Results.Problem("Okänd medlem", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);

            var net = BalanceCalculator.NetWith(me.Value, person, entries, closures);

            var items = new List<SettleEntryDto>();
            foreach (var e in entries.OrderByDescending(e => e.Date))
            {
                long signed = 0;
                foreach (var d in BalanceCalculator.OpenDebts(e, closures))
                {
                    if (d.Debtor == person && d.Creditor == me) signed += d.AmountMinor;
                    else if (d.Debtor == me && d.Creditor == person) signed -= d.AmountMinor;
                }
                if (signed != 0)
                    items.Add(new SettleEntryDto(e.Id, e.Title, e.Date, signed));
            }

            // Swish pre-fill link (swish-settlement-payments spec): a convenience launcher for the
            // acting DEBTOR only. net < 0 means the acting user owes `other` (BalanceCalculator sign
            // convention). Swish is SEK-only, and the creditor must have saved a phone number
            // (ADR-0026 — the member's single number doubles as the Swish payee). The URL is built
            // server-side (ADR-0006); the amount is the absolute debt.
            SwishPayDto? swishPay = null;
            if (net < 0
                && string.Equals(data.Household.Currency, "SEK", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(other.PhoneNumber))
            {
                var amountMinor = -net;
                var uri = SwishLink.Build(other.PhoneNumber, amountMinor, data.Household.Name);
                swishPay = new SwishPayDto(uri, amountMinor);
            }

            return Results.Ok(new SettlePreviewDto(net, Labels.Relation(net), other.Name, items, swishPay));
        }).WithName("GetSettlePreview")
            .Produces<SettlePreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}/settlements", async (
            Guid id, Guid person, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (!data.MembersById.ContainsKey(person))
                return Results.Problem("Okänd medlem", statusCode: 404);

            var entries = (await Loaders.LoadEntries(db, id, ct)).ToDictionary(e => e.Id);

            // Order client-side: SQLite can't ORDER BY DateTimeOffset (ADR-0010 portability).
            var settlements = (await db.Settlements
                .Where(s => s.HouseholdId == id)
                .Include(s => s.Closures)
                .ToListAsync(ct))
                .OrderByDescending(s => s.SettledAt);

            var meId = me.Value;
            var items = new List<SettlementHistoryItemDto>();

            foreach (var s in settlements)
            {
                long net = 0;
                var rows = new List<SettlementHistoryEntryDto>();

                foreach (var c in s.Closures)
                {
                    var involvesPair =
                        (c.DebtorMemberId == person && c.CreditorMemberId == meId) ||
                        (c.DebtorMemberId == meId && c.CreditorMemberId == person);
                    if (!involvesPair) continue;
                    if (!entries.TryGetValue(c.EntryId, out var entry)) continue;

                    // The closed debt's amount comes from the entry's frozen shares — never stored
                    // on the closure (ADR-0007). A pair closes at most one debt per entry.
                    var amount = BalanceCalculator.Debts(entry)
                        .Where(d => d.Debtor == c.DebtorMemberId && d.Creditor == c.CreditorMemberId)
                        .Sum(d => d.AmountMinor);

                    // Signed toward the viewer: +person owed me, −I owed person.
                    net += c.CreditorMemberId == meId ? amount : -amount;
                    rows.Add(new SettlementHistoryEntryDto(entry.Id, entry.Title, entry.Date, amount));
                }

                if (rows.Count == 0) continue; // this settlement didn't touch the pair

                var ordered = rows.OrderByDescending(r => r.Date).ToList();
                items.Add(new SettlementHistoryItemDto(
                    s.Id, s.SettledAt, net, s.InitiatedByMemberId, ordered.Count, ordered));
            }

            return Results.Ok(items);
        }).WithName("GetSettlementHistory")
            .Produces<IReadOnlyList<SettlementHistoryItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households/{id:guid}/settlements", async (
            Guid id, CreateSettlementRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (!data.MembersById.ContainsKey(req.PersonMemberId))
                return Results.Problem("Okänd medlem", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);

            var settlement = new Settlement
            {
                Id = Guid.NewGuid(),
                HouseholdId = id,
                SettledAt = DateTimeOffset.UtcNow,
                InitiatedByMemberId = me.Value
            };

            foreach (var e in entries)
                foreach (var d in BalanceCalculator.OpenDebts(e, closures))
                {
                    var involvesPair =
                        (d.Debtor == me && d.Creditor == req.PersonMemberId) ||
                        (d.Debtor == req.PersonMemberId && d.Creditor == me);
                    if (involvesPair)
                        settlement.Closures.Add(new SettlementClosure
                        {
                            Id = Guid.NewGuid(),
                            EntryId = e.Id,
                            DebtorMemberId = d.Debtor,
                            CreditorMemberId = d.Creditor
                        });
                }

            db.Settlements.Add(settlement);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/households/{id}/settlements/{settlement.Id}",
                new CreateSettlementResponse(settlement.Id));
        }).WithName("CreateSettlement")
            .Produces<CreateSettlementResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
