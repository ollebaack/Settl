using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

/// <summary>
/// Trust notifications (trust-notifications-v1): the in-app stream of "someone
/// changed something that affects your money". Projected on read from the append-only
/// <see cref="LedgerEvent"/> log — nothing is stored per recipient. Unread state is the
/// caller's <see cref="Member.NotificationsSeenAt"/> cursor.
/// </summary>
public static class NotificationsEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Sv = CultureInfo.GetCultureInfo("sv-SE");

    // Cap the read projection — a household's stream is bounded in practice and the newest
    // events are all the "were you cheated?" signal needs.
    private const int MaxEvents = 200;

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/notifications", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(
                m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            var data = await Loaders.LoadHousehold(db, id, ct);
            if (data is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            var seenAt = data.MembersById.TryGetValue(me.Value, out var meMember)
                ? meMember.NotificationsSeenAt
                : null;

            // SQLite can't ORDER BY DateTimeOffset (provider portability, ADR-0010), so materialize
            // the household's events and sort/cap on the client — the same pattern GetEntries uses.
            var events = await db.LedgerEvents
                .Where(e => e.HouseholdId == id)
                .ToListAsync(ct);

            var mine = events
                .Where(e => Concerns(e, me.Value))
                .OrderByDescending(e => e.OccurredAt)
                .Take(MaxEvents)
                .Select(e => ToDto(e, data, seenAt))
                .ToList();

            var unread = mine.Count(n => n.IsUnread);
            return Results.Ok(new NotificationListDto(unread, mine));
        }).WithName("GetNotifications")
            .Produces<NotificationListDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households/{id:guid}/notifications/seen", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(
                m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            var member = await db.Members.FirstAsync(m => m.Id == me, ct);
            member.NotificationsSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithName("MarkNotificationsSeen")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static bool Concerns(LedgerEvent e, Guid me)
    {
        var meStr = me.ToString();
        foreach (var part in e.AffectedMemberIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (part == meStr) return true;
        return false;
    }

    private static NotificationDto ToDto(LedgerEvent e, HouseholdData data, DateTimeOffset? seenAt)
    {
        var payload = JsonSerializer.Deserialize<LedgerEventPayload>(e.PayloadJson, Json)
                      ?? new LedgerEventPayload();

        // Actor name: prefer the live member, fall back to the denormalized snapshot (survives
        // the actor later leaving the household).
        var actorName = data.MembersById.TryGetValue(e.ActorMemberId, out var actor)
            ? actor.Name
            : payload.ActorName;

        var changes = payload.Changes.Select(RenderChange).ToList();

        return new NotificationDto(
            e.Id,
            Contract.LedgerEventType(e.Type),
            e.ActorMemberId,
            actorName,
            payload.Title,
            payload.AmountMinor,
            e.EntryId,
            e.RecurringTemplateId,
            changes,
            e.OccurredAt,
            IsUnread: seenAt is null || e.OccurredAt > seenAt.Value);
    }

    /// <summary>Turns a stored raw before/after into a display-ready sv-SE change line. Keeping
    /// this at read time (not write time) is what lets copy change without rewriting history.</summary>
    private static NotificationChangeDto RenderChange(LedgerFieldChange c) => c.Field switch
    {
        "amount" => new NotificationChangeDto("amount", "Belopp", FormatMinor(c.Before), FormatMinor(c.After)),
        "payer" => new NotificationChangeDto("payer", "Betalare", c.Before, c.After),
        "split" => new NotificationChangeDto("split", "Delning", SplitLabel(c.Before), SplitLabel(c.After)),
        "cadence" => new NotificationChangeDto("cadence", "Intervall", CadenceLabel(c.Before), CadenceLabel(c.After)),
        "date" => new NotificationChangeDto("date", "Nästa datum", FormatDate(c.Before), FormatDate(c.After)),
        _ => new NotificationChangeDto(c.Field, c.Field, c.Before, c.After)
    };

    private static string? FormatMinor(string? minor) =>
        long.TryParse(minor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? Money.FormatKr(v)
            : minor;

    private static string? FormatDate(string? iso) =>
        DateOnly.TryParse(iso, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("d MMM yyyy", Sv)
            : iso;

    private static string? SplitLabel(string? wire) => wire switch
    {
        "equal" => "Lika",
        "percent" => "Procent",
        "amount" => "Belopp",
        "none" => "Ingen",
        _ => wire
    };

    private static string? CadenceLabel(string? wire) => wire switch
    {
        "monthly" => "Månadsvis",
        "biweekly" => "Varannan vecka",
        "weekly" => "Veckovis",
        _ => wire
    };
}
