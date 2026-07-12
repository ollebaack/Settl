using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class EntriesEndpoints
{
    public static IEndpointRouteBuilder MapEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/entries", async (
            Guid id, string? type, int? limit, string? sort,
            ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);
            var titles = await Loaders.LoadTemplateTitles(db, id, ct);

            IEnumerable<Entry> query = entries;
            if (!string.IsNullOrWhiteSpace(type))
            {
                var filter = type.Trim().ToLowerInvariant() switch
                {
                    "expense" => (EntryType?)EntryType.Expense,
                    "iou" => EntryType.Iou,
                    "recurring" => EntryType.RecurringPost,
                    _ => null
                };
                if (filter is not null) query = query.Where(e => e.Type == filter);
            }

            // Default sort: date desc, then createdAt desc for stability.
            query = query.OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt);
            if (limit is > 0) query = query.Take(limit.Value);

            var dtos = query
                .Select(e => Mapping.ToEntryDto(e, data.OrderedMemberIds, data.MembersById, titles, closures, me.Value))
                .ToList();
            return Results.Ok(dtos);
        }).WithName("GetEntries");

        app.MapPost("/households/{id:guid}/entries", async (
            Guid id, CreateEntryRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTimeOffset.UtcNow;

            try
            {
                var entry = BuildEntryFromRequest(req.Type, req.Title, req.AmountMinor, req.Date, req.PaidByMemberId,
                    req.FromMemberId, req.ToMemberId, req.Split, data, id, today, now, Guid.NewGuid());
                db.Entries.Add(entry);
                await db.SaveChangesAsync(ct);

                var titles = await Loaders.LoadTemplateTitles(db, id, ct);
                var closures = await Loaders.LoadClosures(db, id, ct);
                var dto = Mapping.ToEntryDto(entry, data.OrderedMemberIds, data.MembersById, titles, closures, me.Value);
                return Results.Created($"/entries/{entry.Id}", dto);
            }
            catch (SplitValidationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).WithName("CreateEntry");

        app.MapGet("/entries/{id:guid}", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var dto = await LoadEntryDto(db, id, me.Value, ct);
            return dto is null ? Results.Problem("Posten hittades inte", statusCode: 404) : Results.Ok(dto);
        }).WithName("GetEntry");

        app.MapPut("/entries/{id:guid}", async (
            Guid id, UpdateEntryRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var entry = await db.Entries.Include(e => e.Shares).FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry is null) return Results.Problem("Posten hittades inte", statusCode: 404);

            var lockedByClosure = await db.SettlementClosures.AnyAsync(c => c.EntryId == id, ct);
            if (lockedByClosure)
                return Results.Problem("Posten är låst — öppna den igen innan du ändrar.", statusCode: 409);

            var data = await Loaders.LoadHousehold(db, entry.HouseholdId, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            try
            {
                var rebuilt = BuildEntryFromRequest(req.Type, req.Title, req.AmountMinor, req.Date, req.PaidByMemberId,
                    req.FromMemberId, req.ToMemberId, req.Split, data, entry.HouseholdId, today, entry.CreatedAt, entry.Id);

                entry.Type = rebuilt.Type;
                entry.Title = rebuilt.Title;
                entry.AmountMinor = rebuilt.AmountMinor;
                entry.Date = rebuilt.Date;
                entry.PaidByMemberId = rebuilt.PaidByMemberId;
                entry.FromMemberId = rebuilt.FromMemberId;
                entry.ToMemberId = rebuilt.ToMemberId;
                entry.SplitMode = rebuilt.SplitMode;

                db.EntryShares.RemoveRange(entry.Shares);
                entry.Shares = rebuilt.Shares;

                await db.SaveChangesAsync(ct);

                var dto = await LoadEntryDto(db, id, me.Value, ct);
                return Results.Ok(dto);
            }
            catch (SplitValidationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).WithName("UpdateEntry");

        app.MapDelete("/entries/{id:guid}", async (Guid id, SettlDbContext db, CancellationToken ct) =>
        {
            var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry is null) return Results.Problem("Posten hittades inte", statusCode: 404);

            if (await db.SettlementClosures.AnyAsync(c => c.EntryId == id, ct))
                return Results.Problem("Posten är låst — öppna den igen innan du tar bort.", statusCode: 409);

            db.Entries.Remove(entry);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteEntry");

        app.MapPost("/entries/{id:guid}/settlements", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var entry = await db.Entries.Include(e => e.Shares).FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry is null) return Results.Problem("Posten hittades inte", statusCode: 404);

            var closures = new ClosureLookup(
                await db.SettlementClosures.Where(c => c.EntryId == id).ToListAsync(ct));
            var open = BalanceCalculator.OpenDebts(entry, closures);

            if (open.Count > 0)
            {
                var settlement = new Settlement
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = entry.HouseholdId,
                    SettledAt = DateTimeOffset.UtcNow,
                    InitiatedByMemberId = me.Value
                };
                foreach (var d in open)
                    settlement.Closures.Add(new SettlementClosure
                    {
                        Id = Guid.NewGuid(),
                        EntryId = id,
                        DebtorMemberId = d.Debtor,
                        CreditorMemberId = d.Creditor
                    });
                db.Settlements.Add(settlement);
                await db.SaveChangesAsync(ct);
            }

            var dto = await LoadEntryDto(db, id, me.Value, ct);
            return Results.Ok(dto);
        }).WithName("SettleEntry");

        app.MapDelete("/entries/{id:guid}/settlements", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entry is null) return Results.Problem("Posten hittades inte", statusCode: 404);

            var closures = await db.SettlementClosures.Where(c => c.EntryId == id).ToListAsync(ct);
            if (closures.Count > 0)
            {
                var settlementIds = closures.Select(c => c.SettlementId).Distinct().ToList();
                db.SettlementClosures.RemoveRange(closures);
                await db.SaveChangesAsync(ct);

                // Remove settlements left with no closures.
                var empties = await db.Settlements
                    .Where(s => settlementIds.Contains(s.Id) && !s.Closures.Any())
                    .ToListAsync(ct);
                if (empties.Count > 0)
                {
                    db.Settlements.RemoveRange(empties);
                    await db.SaveChangesAsync(ct);
                }
            }

            var dto = await LoadEntryDto(db, id, me.Value, ct);
            return Results.Ok(dto);
        }).WithName("ReopenEntry");

        return app;
    }

    private static async Task<EntryDto?> LoadEntryDto(SettlDbContext db, Guid entryId, Guid me, CancellationToken ct)
    {
        var entry = await db.Entries.Include(e => e.Shares).FirstOrDefaultAsync(e => e.Id == entryId, ct);
        if (entry is null) return null;

        var data = await Loaders.LoadHousehold(db, entry.HouseholdId, ct);
        if (data is null) return null;

        var closures = await Loaders.LoadClosures(db, entry.HouseholdId, ct);
        var titles = await Loaders.LoadTemplateTitles(db, entry.HouseholdId, ct);
        return Mapping.ToEntryDto(entry, data.OrderedMemberIds, data.MembersById, titles, closures, me);
    }

    private static Entry BuildEntryFromRequest(
        string type, string? title, long amountMinor, DateOnly? date,
        Guid? paidByMemberId, Guid? fromMemberId, Guid? toMemberId, SplitInput? split,
        HouseholdData data, Guid householdId, DateOnly today, DateTimeOffset createdAt, Guid entryId)
    {
        if (amountMinor <= 0)
            throw new SplitValidationException("Ange ett belopp först");

        var normalizedType = type?.Trim().ToLowerInvariant();
        var effectiveDate = date ?? today;

        if (normalizedType == "iou")
        {
            if (fromMemberId is null || toMemberId is null)
                throw new SplitValidationException("Lån kräver både från och till");
            if (fromMemberId == toMemberId)
                throw new SplitValidationException("Från och till måste vara olika");
            if (!data.MembersById.ContainsKey(fromMemberId.Value) || !data.MembersById.ContainsKey(toMemberId.Value))
                throw new SplitValidationException("Okänd medlem");

            return new Entry
            {
                Id = entryId,
                HouseholdId = householdId,
                Type = EntryType.Iou,
                Title = string.IsNullOrWhiteSpace(title) ? "Lån" : title!.Trim(),
                AmountMinor = amountMinor,
                Date = effectiveDate,
                CreatedAt = createdAt,
                FromMemberId = fromMemberId,
                ToMemberId = toMemberId,
                SplitMode = SplitMode.None
            };
        }

        if (normalizedType != "expense")
            throw new SplitValidationException("Ogiltig posttyp");

        if (paidByMemberId is null || !data.MembersById.ContainsKey(paidByMemberId.Value))
            throw new SplitValidationException("Okänd betalare");

        var mode = split is null ? SplitMode.Equal : Contract.ParseSplitMode(split.Mode);
        if (mode == SplitMode.None) mode = SplitMode.Equal;

        var formula = split?.Values ?? new Dictionary<Guid, decimal>();
        var frozen = ShareFreezer.Freeze(mode, data.OrderedMemberIds, amountMinor, formula);

        var entry = new Entry
        {
            Id = entryId,
            HouseholdId = householdId,
            Type = EntryType.Expense,
            Title = string.IsNullOrWhiteSpace(title) ? "Utan titel" : title!.Trim(),
            AmountMinor = amountMinor,
            Date = effectiveDate,
            CreatedAt = createdAt,
            PaidByMemberId = paidByMemberId,
            SplitMode = mode
        };
        foreach (var s in frozen)
            entry.Shares.Add(new EntryShare
            { EntryId = entry.Id, MemberId = s.MemberId, ShareMinor = s.ShareMinor, FormulaValue = s.FormulaValue });
        return entry;
    }
}
