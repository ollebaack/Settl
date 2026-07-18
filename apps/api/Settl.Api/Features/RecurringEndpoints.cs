using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class RecurringEndpoints
{
    public static IEndpointRouteBuilder MapRecurringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/recurring", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var templates = await db.RecurringTemplates
                .Where(t => t.HouseholdId == id)
                .Include(t => t.Shares)
                .ToListAsync(ct);

            var dtos = templates
                .Select(t => Mapping.ToRecurringDto(t, data.OrderedMemberIds, data.MembersById, me.Value, today))
                // Ended templates sink below live ones; within each group, soonest next-post first.
                .OrderBy(t => t.Ended)
                .ThenBy(t => t.DaysUntil)
                .ToList();

            // Monthly-normalized tiles count only templates that still cost something — active and
            // not yet ended (a template past its EndDate contributes nothing going forward).
            long recTotal = 0, recShare = 0;
            foreach (var t in templates.Where(t => t.Active && !RecurrenceCalculator.IsEnded(t.NextPostDate, t.EndDate)))
            {
                recTotal += RecurrenceCalculator.MonthlyNormalizedMinor(t.AmountMinor, t.Cadence);
                var myShare = Mapping.TemplateShares(t, data.OrderedMemberIds).Where(s => s.MemberId == me).Sum(s => s.ShareMinor);
                recShare += RecurrenceCalculator.MonthlyNormalizedMinor(myShare, t.Cadence);
            }

            return Results.Ok(new RecurringListDto(recTotal, recShare, dtos));
        }).WithName("GetRecurringTemplates")
            .Produces<RecurringListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households/{id:guid}/recurring", async (
            Guid id, CreateRecurringRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            if (req.AmountMinor <= 0) return Results.Problem("Ange ett belopp först", statusCode: 400);
            if (!data.MembersById.ContainsKey(req.PaidByMemberId))
                return Results.Problem("Okänd betalare", statusCode: 400);

            try
            {
                var mode = Contract.ParseSplitMode(req.Split.Mode);
                if (mode == SplitMode.None) mode = SplitMode.Equal;
                var formula = req.Split.Values ?? new Dictionary<Guid, decimal>();

                // Validate formula up front (throws on tolerance breach).
                ShareFreezer.Freeze(mode, data.OrderedMemberIds, req.AmountMinor, formula);

                var templateTitle = string.IsNullOrWhiteSpace(req.Title) ? "Utan titel" : req.Title!.Trim();
                var cadence = Contract.ParseCadence(req.Cadence);
                var endDate = ResolveEndDate(req.EndMode, req.EndDate, req.EndAfterCount, req.NextPostDate, cadence);
                var template = new RecurringTemplate
                {
                    Id = Guid.NewGuid(),
                    HouseholdId = id,
                    Title = templateTitle,
                    Category = CategoryClassifier.Classify(templateTitle),
                    AmountMinor = req.AmountMinor,
                    Cadence = cadence,
                    NextPostDate = req.NextPostDate,
                    EndDate = endDate,
                    PaidByMemberId = req.PaidByMemberId,
                    SplitMode = mode,
                    Active = true,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                foreach (var m in data.OrderedMemberIds)
                    template.Shares.Add(new RecurringShare
                    {
                        RecurringTemplateId = template.Id,
                        MemberId = m,
                        FormulaValue = mode == SplitMode.Equal ? null : formula.TryGetValue(m, out var v) ? v : 0m
                    });

                db.RecurringTemplates.Add(template);
                await db.SaveChangesAsync(ct);

                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var dto = Mapping.ToRecurringDto(template, data.OrderedMemberIds, data.MembersById, me.Value, today);
                return Results.Created($"/recurring/{template.Id}", dto);
            }
            catch (SplitValidationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).WithName("CreateRecurringTemplate")
            .Produces<RecurringDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/recurring/{id:guid}", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var template = await db.RecurringTemplates.Include(t => t.Shares).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template is null) return Results.Problem("Den återkommande posten hittades inte", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, template.HouseholdId, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var dto = Mapping.ToRecurringDto(template, data.OrderedMemberIds, data.MembersById, me.Value, today);

            var frozen = Mapping.TemplateShares(template, data.OrderedMemberIds);
            var shareRows = frozen
                .Where(s => s.ShareMinor > 0)
                .Select(s => new RecurringShareRowDto(
                    s.MemberId, Mapping.Name(data.MembersById, s.MemberId), s.ShareMinor, s.MemberId == template.PaidByMemberId))
                .ToList();

            var posts = await db.Entries
                .Where(e => e.RecurringTemplateId == id)
                .Include(e => e.Shares)
                .OrderByDescending(e => e.Date)
                .ToListAsync(ct);
            var closures = await Loaders.LoadClosures(db, template.HouseholdId, ct);
            var postedEntries = posts
                .Select(e => new PostedEntrySummaryDto(e.Id, e.Title, e.AmountMinor, BalanceCalculator.IsSettled(e, closures)))
                .ToList();

            return Results.Ok(new RecurringDetailDto(dto, shareRows, postedEntries));
        }).WithName("GetRecurringTemplate")
            .Produces<RecurringDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPatch("/recurring/{id:guid}", async (
            Guid id, UpdateRecurringRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var template = await db.RecurringTemplates.Include(t => t.Shares).FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template is null) return Results.Problem("Den återkommande posten hittades inte", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, template.HouseholdId, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            // Snapshot the schedule/amount fields before mutating, for the trust event diff
            // (trust-notifications-v1).
            var oldAmount = template.AmountMinor;
            var oldCadence = template.Cadence;
            var oldNextPostDate = template.NextPostDate;

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            try
            {
                var wasActive = template.Active;
                if (req.Active is not null) template.Active = req.Active.Value;
                if (!string.IsNullOrWhiteSpace(req.Title))
                {
                    template.Title = req.Title!.Trim();
                    template.Category = CategoryClassifier.Classify(template.Title);
                }
                if (req.AmountMinor is { } amt)
                {
                    if (amt <= 0) return Results.Problem("Ange ett belopp först", statusCode: 400);
                    template.AmountMinor = amt;
                }
                if (!string.IsNullOrWhiteSpace(req.Cadence)) template.Cadence = Contract.ParseCadence(req.Cadence);
                if (req.NextPostDate is { } next) template.NextPostDate = next;
                if (req.PaidByMemberId is { } payer)
                {
                    if (!data.MembersById.ContainsKey(payer)) return Results.Problem("Okänd betalare", statusCode: 400);
                    template.PaidByMemberId = payer;
                }

                if (req.Split is not null)
                {
                    var mode = Contract.ParseSplitMode(req.Split.Mode);
                    if (mode == SplitMode.None) mode = SplitMode.Equal;
                    var formula = req.Split.Values ?? new Dictionary<Guid, decimal>();
                    ShareFreezer.Freeze(mode, data.OrderedMemberIds, template.AmountMinor, formula); // validate

                    template.SplitMode = mode;
                    db.RecurringShares.RemoveRange(template.Shares);
                    template.Shares = new List<RecurringShare>();
                    foreach (var m in data.OrderedMemberIds)
                        template.Shares.Add(new RecurringShare
                        {
                            RecurringTemplateId = template.Id,
                            MemberId = m,
                            FormulaValue = mode == SplitMode.Equal ? null : formula.TryGetValue(m, out var v) ? v : 0m
                        });
                }
                else
                {
                    // No new split supplied: the stored formula must still reconcile with the
                    // (possibly changed) amount. For Amount mode a changed amount would otherwise
                    // leave frozen shares that never sum to the total — wedging every later freeze
                    // (reads + the background poster). Re-validate now → 400 if inconsistent.
                    var effectiveFormula = template.Shares.ToDictionary(s => s.MemberId, s => s.FormulaValue ?? 0m);
                    ShareFreezer.Freeze(template.SplitMode, data.OrderedMemberIds, template.AmountMinor, effectiveFormula);
                }

                // Termination: null EndMode leaves the end date untouched; otherwise re-resolve from
                // the (possibly updated) cadence + next-post cursor. "count" counts from the next
                // upcoming post, since there's no immutable start stored.
                if (req.EndMode is not null)
                    template.EndDate = ResolveEndDate(
                        req.EndMode, req.EndDate, req.EndAfterCount, template.NextPostDate, template.Cadence);

                // Resume (paused → active): skip the paused gap by scheduling the next cycle on or
                // after today, rather than back-posting every missed cycle in one burst.
                if (!wasActive && template.Active)
                    template.NextPostDate = RecurrenceCalculator.FastForwardToOnOrAfter(
                        template.NextPostDate, template.Cadence, today);

                var changes = new List<LedgerFieldChange>();
                if (oldAmount != template.AmountMinor)
                    changes.Add(new LedgerFieldChange("amount",
                        oldAmount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        template.AmountMinor.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                if (oldCadence != template.Cadence)
                    changes.Add(new LedgerFieldChange("cadence",
                        Contract.Cadence(oldCadence), Contract.Cadence(template.Cadence)));
                if (oldNextPostDate != template.NextPostDate)
                    changes.Add(new LedgerFieldChange("date",
                        oldNextPostDate.ToString("O"), template.NextPostDate.ToString("O")));
                LedgerEventLog.RecordRecurringChanged(db, data, me.Value, template, changes);

                await db.SaveChangesAsync(ct);

                var dto = Mapping.ToRecurringDto(template, data.OrderedMemberIds, data.MembersById, me.Value, today);
                return Results.Ok(dto);
            }
            catch (SplitValidationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        }).WithName("UpdateRecurringTemplate")
            .Produces<RecurringDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Delete-if-clean, else deactivate (ADR-0018). A template that has posted zero entries
        // can be hard-deleted (its RecurringShares cascade). Once it has posted history the debts
        // those cycles created are real, so deletion is refused with 409 — the caller pauses
        // (PATCH { active:false }) instead, or deletes the individual posted entries in the ledger.
        app.MapDelete("/recurring/{id:guid}", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var template = await db.RecurringTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (template is null) return Results.Problem("Den återkommande posten hittades inte", statusCode: 404);

            if (await db.Entries.AnyAsync(e => e.RecurringTemplateId == id, ct))
                return Results.Problem("Den återkommande posten har bokförda perioder", statusCode: 409);

            db.RecurringTemplates.Remove(template);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteRecurringTemplate")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// Resolves the mutually-exclusive termination input to a single inclusive <c>EndDate</c>
    /// (recurring-end-date spec). "never"/null → no end; "date" → the given date (must be on or
    /// after the next post); "count" → the Nth post date from <paramref name="nextPostDate"/>.
    /// Throws <see cref="SplitValidationException"/> (→ 400) on invalid input.
    /// </summary>
    private static DateOnly? ResolveEndDate(
        string? endMode, DateOnly? endDate, int? endAfterCount, DateOnly nextPostDate, Domain.Cadence cadence)
        => (endMode ?? "never").Trim().ToLowerInvariant() switch
        {
            "never" => null,
            "date" => endDate is { } d
                ? d.DayNumber >= nextPostDate.DayNumber
                    ? d
                    : throw new SplitValidationException("Slutdatumet kan inte vara före nästa bokföring")
                : throw new SplitValidationException("Välj ett slutdatum"),
            "count" => endAfterCount is { } n && n >= 1
                ? RecurrenceCalculator.NthPostDate(nextPostDate, cadence, n)
                : throw new SplitValidationException("Ange hur många gånger den ska bokföras"),
            _ => throw new SplitValidationException("Ogiltigt slutläge")
        };
}
