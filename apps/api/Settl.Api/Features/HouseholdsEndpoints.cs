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
        app.MapGet("/households", async (
            ICurrentUserAccessor cu, SettlDbContext db, bool? includeArchived, CancellationToken ct) =>
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
                // Archived households are hidden from the normal list (household-ownership spec) and only
                // returned when the caller explicitly asks (the "Arkiverade" section).
                if (data.Household.ArchivedAt is not null && includeArchived != true) continue;

                var entries = await Loaders.LoadEntries(db, hid, ct);
                var closures = await Loaders.LoadClosures(db, hid, ct);

                var overall = OverallNet(me.Value, data, entries, closures);
                result.Add(new HouseholdListItemDto(
                    data.Household.Id,
                    data.Household.Name,
                    data.Household.Currency,
                    data.OrderedMembers.Select(m => m.Name).ToList(),
                    overall,
                    Labels.Net(overall),
                    data.Household.OwnerMemberId,
                    data.Household.OwnerMemberId == me.Value,
                    data.Household.ArchivedAt));
            }

            return Results.Ok(result);
        }).WithName("GetHouseholds")
            .Produces<List<HouseholdListItemDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households", async (
            CreateHouseholdRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.Problem("Namn krävs", statusCode: 400);

            var now = DateTimeOffset.UtcNow;
            var household = new Household
            {
                Id = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Currency = string.IsNullOrWhiteSpace(req.Currency) ? "SEK" : req.Currency!.Trim(),
                CreatedAt = now,
                OwnerMemberId = me.Value  // the creator owns the household (household-ownership spec)
            };
            db.Households.Add(household);
            db.HouseholdMemberships.Add(new HouseholdMembership
            { HouseholdId = household.Id, MemberId = me.Value, JoinedAt = now });

            await db.SaveChangesAsync(ct);

            var data = await Loaders.LoadHousehold(db, household.Id, ct);
            return Results.Created($"/households/{household.Id}", ToHouseholdDto(data!, me.Value));
        }).WithName("CreateHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            return Results.Ok(ToHouseholdDto(data, me.Value));
        }).WithName("GetHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/households/{id:guid}/members", async (Guid id, SettlDbContext db, CancellationToken ct) =>
        {
            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
            return Results.Ok(data.OrderedMembers.Select(m => new MemberDto(m.Id, m.Name, m.AvatarColor, m.AvatarEmoji)));
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
                    x, Mapping.Name(data.MembersById, x), data.MembersById[x].AvatarColor,
                    data.MembersById[x].AvatarEmoji, net, Labels.Relation(net));
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

        // Per-person "who paid how much, when" for the Statistik page
        // (docs/specs/household-statistics.md). Aggregated server-side per ADR-0006 and,
        // per ADR-0010, in memory (LINQ-to-objects) rather than provider-specific
        // date-grouping SQL. Range defaults to the trailing 12 whole months.
        app.MapGet("/households/{id:guid}/stats/contributions", async (
            Guid id, DateOnly? from, DateOnly? to,
            ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            // Don't leak existence of households the caller isn't in.
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);

            // Default window: the current month plus the 11 before it. Bounds are aligned
            // to month starts; [rangeFrom, rangeTo) is half-open (rangeTo exclusive).
            var currentMonth = FirstOfMonth(DateOnly.FromDateTime(DateTime.UtcNow));
            var rangeTo = to is { } t ? FirstOfMonth(t).AddMonths(1) : currentMonth.AddMonths(1);
            var rangeFrom = from is { } f ? FirstOfMonth(f) : rangeTo.AddMonths(-12);

            var entries = await Loaders.LoadEntries(db, id, ct);

            // On the default window (no explicit `from`), don't render a flat empty runway
            // for young households: clip the start forward to just before the first attributable
            // payment's month. We keep one zero-filled lead-in month so the series rises from 0
            // rather than starting mid-air — and a one-month-old household renders a visible line
            // instead of a lone (dot-less) point. Clamped to the trailing 12-month floor, so
            // established households whose first payment predates it stay pinned to that cap.
            if (from is null)
            {
                DateOnly? firstPaid = entries
                    .Where(e => e.PaidByMemberId is not null && e.Date < rangeTo)
                    .Select(e => (DateOnly?)e.Date)
                    .DefaultIfEmpty(null)
                    .Min();
                if (firstPaid is { } fp && FirstOfMonth(fp) > rangeFrom)
                {
                    var leadIn = FirstOfMonth(fp).AddMonths(-1);
                    rangeFrom = leadIn > rangeFrom ? leadIn : rangeFrom;
                }
            }

            // Sum by (payer, month) over both Expense and RecurringPost — recurring posts
            // are real spend. Entries without a payer (SplitMode.None) can't be attributed.
            var paid = entries
                .Where(e => e.PaidByMemberId is not null
                    && e.Date >= rangeFrom && e.Date < rangeTo)
                .GroupBy(e => (Member: e.PaidByMemberId!.Value, Month: MonthKey(e.Date)))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.AmountMinor));

            // Series = members with any contribution in range, in household order.
            var members = data.OrderedMemberIds
                .Where(mid => data.MembersById.ContainsKey(mid)
                    && paid.Keys.Any(k => k.Member == mid))
                .Select(mid =>
                {
                    var m = data.MembersById[mid];
                    return new ContributionMemberDto(mid, m.Name, m.AvatarColor, m.AvatarEmoji);
                })
                .ToList();

            // Continuous, zero-filled month series so the chart axis has no gaps.
            var buckets = new List<ContributionBucketDto>();
            for (var month = rangeFrom; month < rangeTo; month = month.AddMonths(1))
            {
                var key = MonthKey(month);
                var perMember = members
                    .Select(m => new MemberContributionDto(
                        m.MemberId,
                        paid.TryGetValue((m.MemberId, key), out var v) ? v : 0))
                    .ToList();
                buckets.Add(new ContributionBucketDto(key, perMember));
            }

            return Results.Ok(new ContributionStatsDto(data.Household.Currency, members, buckets));
        }).WithName("GetContributionStats")
            .Produces<ContributionStatsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ---- Ownership & archival (docs/specs/household-ownership.md) ----

        // Debt figures + guard flags for the leave/archive confirmation sheets. Debts warn,
        // never block, so this is purely informational.
        app.MapGet("/households/{id:guid}/removal-preview", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            // Don't leak existence of households the caller isn't in.
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var entries = await Loaders.LoadEntries(db, id, ct);
            var closures = await Loaders.LoadClosures(db, id, ct);

            var isOwner = data.Household.OwnerMemberId == me.Value;
            var memberCount = data.OrderedMemberIds.Count;

            var viewerOpenDebts = data.OrderedMemberIds
                .Where(x => x != me.Value)
                .Select(x =>
                {
                    var net = BalanceCalculator.NetWith(me.Value, x, entries, closures);
                    return new PersonBalanceDto(
                        x, Mapping.Name(data.MembersById, x), data.MembersById[x].AvatarColor,
                        data.MembersById[x].AvatarEmoji, net, Labels.Relation(net));
                })
                .Where(p => p.NetMinor != 0)
                .ToList();

            return Results.Ok(new RemovalPreviewDto(
                isOwner,
                memberCount,
                SoleMember: memberCount == 1,
                MustTransferFirst: isOwner && memberCount > 1,
                viewerOpenDebts,
                BalanceCalculator.HouseholdOpenTotalMinor(entries, closures),
                IsEmpty: !await HasActivity(db, id, ct)));
        }).WithName("GetHouseholdRemovalPreview")
            .Produces<RemovalPreviewDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Reassign ownership to another current member (owner-only). The previous owner
        // becomes an ordinary member. Never automatic (household-ownership spec).
        app.MapPost("/households/{id:guid}/transfer-ownership", async (
            Guid id, TransferOwnershipRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (data.Household.OwnerMemberId != me.Value)
                return Results.Problem("Bara ägaren kan överföra ägarskapet", statusCode: 403);
            if (req.NewOwnerMemberId == me.Value)
                return Results.Problem("Du äger redan hushållet", statusCode: 400);
            if (!data.MembersById.ContainsKey(req.NewOwnerMemberId))
                return Results.Problem("Den nya ägaren måste vara medlem i hushållet", statusCode: 400);

            data.Household.OwnerMemberId = req.NewOwnerMemberId;
            await db.SaveChangesAsync(ct);

            var fresh = await Loaders.LoadHousehold(db, id, ct);
            return Results.Ok(ToHouseholdDto(fresh!, me.Value));
        }).WithName("TransferHouseholdOwnership")
            .Produces<HouseholdDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Leave a household. A non-owner's membership is removed. The owner cannot leave
        // while others remain (must transfer first → 409). A sole owner leaving archives
        // the household and keeps their membership/ownership so they can restore it.
        app.MapPost("/households/{id:guid}/leave", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var isOwner = data.Household.OwnerMemberId == me.Value;
            var memberCount = data.OrderedMemberIds.Count;

            if (memberCount == 1)
            {
                // Sole member (always the owner) — leaving archives the household.
                if (data.Household.ArchivedAt is null)
                    data.Household.ArchivedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(new LeaveResultDto(Archived: true));
            }

            if (isOwner)
                return Results.Problem("Överför ägarskapet innan du lämnar hushållet", statusCode: 409);

            var membership = await db.HouseholdMemberships
                .FirstOrDefaultAsync(m => m.HouseholdId == id && m.MemberId == me.Value, ct);
            if (membership is not null) db.HouseholdMemberships.Remove(membership);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new LeaveResultDto(Archived: false));
        }).WithName("LeaveHousehold")
            .Produces<LeaveResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Soft-archive the whole household (owner-only). Hides it for everyone; retains all
        // data; restorable by the owner (household-ownership spec).
        app.MapPost("/households/{id:guid}/archive", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (data.Household.OwnerMemberId != me.Value)
                return Results.Problem("Bara ägaren kan arkivera hushållet", statusCode: 403);
            if (data.Household.ArchivedAt is not null)
                return Results.Problem("Hushållet är redan arkiverat", statusCode: 409);

            data.Household.ArchivedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            var fresh = await Loaders.LoadHousehold(db, id, ct);
            return Results.Ok(ToHouseholdDto(fresh!, me.Value));
        }).WithName("ArchiveHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Restore an archived household (owner-only).
        app.MapPost("/households/{id:guid}/restore", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (data.Household.OwnerMemberId != me.Value)
                return Results.Problem("Bara ägaren kan återställa hushållet", statusCode: 403);
            if (data.Household.ArchivedAt is null)
                return Results.Problem("Hushållet är inte arkiverat", statusCode: 409);

            data.Household.ArchivedAt = null;
            await db.SaveChangesAsync(ct);

            var fresh = await Loaders.LoadHousehold(db, id, ct);
            return Results.Ok(ToHouseholdDto(fresh!, me.Value));
        }).WithName("RestoreHousehold")
            .Produces<HouseholdDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Permanently delete an EMPTY household (owner-only). Unlike archive this is
        // terminal and cascades away memberships and pending invites — but only when the
        // household has no ledger history to protect (household-ownership spec, a carve-out to its
        // soft-only rule). Extra members don't block (the client warns first); pending
        // invites are cascade-revoked, not activity.
        app.MapDelete("/households/{id:guid}", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null || !data.MembersById.ContainsKey(me.Value))
                return Results.Problem("Hushållet hittades inte", statusCode: 404);
            if (data.Household.OwnerMemberId != me.Value)
                return Results.Problem("Bara ägaren kan ta bort hushållet", statusCode: 403);

            // Re-check emptiness inside the transaction so activity added between the read
            // and the delete can't be silently destroyed — it fails with 409 instead.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (await HasActivity(db, id, ct))
                return Results.Problem(
                    "Hushållet har poster och kan inte tas bort — arkivera det i stället.",
                    statusCode: 409);

            db.Households.Remove(data.Household);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        }).WithName("DeleteHousehold")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    // A household is "empty" (safe to hard-delete) only with no ledger history: no
    // entries, no recurring templates, no settlements (household-ownership spec). Pending invites and
    // extra memberships are cascade-removed and don't count.
    private static async Task<bool> HasActivity(SettlDbContext db, Guid householdId, CancellationToken ct) =>
        await db.Entries.AnyAsync(e => e.HouseholdId == householdId, ct)
        || await db.RecurringTemplates.AnyAsync(t => t.HouseholdId == householdId, ct)
        || await db.Settlements.AnyAsync(s => s.HouseholdId == householdId, ct);

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private static string MonthKey(DateOnly d) => $"{d.Year:D4}-{d.Month:D2}";

    private static long OverallNet(Guid me, HouseholdData data, IReadOnlyList<Entry> entries, ClosureLookup closures) =>
        data.OrderedMemberIds.Where(m => m != me)
            .Sum(x => BalanceCalculator.NetWith(me, x, entries, closures));

    private static HouseholdDto ToHouseholdDto(HouseholdData data, Guid me) =>
        new(data.Household.Id,
            data.Household.Name,
            data.Household.Currency,
            data.OrderedMembers.Select(m => new MemberDto(m.Id, m.Name, m.AvatarColor, m.AvatarEmoji)).ToList(),
            data.Household.OwnerMemberId,
            data.Household.OwnerMemberId == me,
            data.Household.ArchivedAt);
}
