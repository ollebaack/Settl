using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

/// <summary>
/// Contacts &amp; blind invites (ADR-0019). Typing a phone number sends a tokenized SMS invite
/// and reveals NOTHING about whether that number is on Settl — there is deliberately no
/// lookup/registration-status endpoint (no enumeration oracle). A contact edge only ever
/// materialises when an invite is accepted (see <see cref="InvitesEndpoints"/>), so this file
/// only reads the graph and sends invites; it never searches for strangers.
/// </summary>
public static class ContactsEndpoints
{
    /// <summary>Rate-limit policy for the invite-send path. SMS costs money and SMS pumping is a
    /// fraud vector, so throttling ships WITH the channel (ADR-0019; tech-debt/0006 is the
    /// free-email precedent that did not need it). Configured in Program.cs.</summary>
    public const string InviteRateLimitPolicy = "contact-invites";

    public static IEndpointRouteBuilder MapContactsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/contacts", async (ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var contacts = await db.Contacts
                .Where(c => c.OwnerMemberId == me)
                .Include(c => c.ContactMember)
                .ToListAsync(ct);

            var myHouseholdIds = await db.HouseholdMemberships
                .Where(m => m.MemberId == me)
                .Select(m => m.HouseholdId)
                .ToListAsync(ct);

            var result = new List<ContactDto>();
            foreach (var c in contacts)
            {
                // "I N hushåll med dig" — derived server-side (ADR-0006), never in the UI.
                var shared = await db.HouseholdMemberships
                    .CountAsync(m => m.MemberId == c.ContactMemberId && myHouseholdIds.Contains(m.HouseholdId), ct);
                result.Add(new ContactDto(c.ContactMemberId, c.ContactMember.Name, c.ContactMember.AvatarColor, shared));
            }

            return Results.Ok(result.OrderBy(r => r.Name).ToList());
        }).WithName("GetContacts")
            .Produces<List<ContactDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("/contacts/pending", async (ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            // Invites I sent that nobody has accepted yet. Ordered/expiry-filtered client-side:
            // SQLite (tests) can't translate DateTimeOffset comparisons.
            var mine = await db.Invites
                .Where(i => i.InvitedByMemberId == me && i.AcceptedAt == null)
                .ToListAsync(ct);
            var now = DateTimeOffset.UtcNow;

            var pending = mine
                .Where(i => i.ExpiresAt > now)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new PendingInviteDto(
                    i.Id, Contract.InviteChannel(i.Channel), i.PhoneNumber, i.Email, i.CreatedAt, i.ExpiresAt))
                .ToList();

            return Results.Ok(pending);
        }).WithName("GetPendingContactInvites")
            .Produces<List<PendingInviteDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/contacts/invites", async (
            CreateContactInviteRequest req, ICurrentUserAccessor cu, SettlDbContext db,
            ISmsSender sms, IEmailSender emailSender, IConfiguration config, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var channel = (req.Channel ?? "").Trim().ToLowerInvariant();
            if (channel is not ("sms" or "email"))
                return Results.Problem("Ogiltig inbjudningskanal", statusCode: 400);

            // Optional household attachment (the "invite to household from contacts" flow).
            Guid? householdId = null;
            string? householdName = null;
            if (req.HouseholdId is Guid hh)
            {
                var household = await db.Households.FirstOrDefaultAsync(h => h.Id == hh, ct);
                if (household is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);
                var isMember = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == hh && m.MemberId == me, ct);
                if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);
                householdId = hh;
                householdName = household.Name;
            }

            var inviter = await db.Members.FirstAsync(m => m.Id == me, ct);
            var now = DateTimeOffset.UtcNow;
            var rawToken = InviteTokens.NewRawToken();
            var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
            var acceptUrl = $"{baseUrl}/accept-invite?token={rawToken}";

            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                HouseholdId = householdId,
                TokenHash = InviteTokens.Hash(rawToken),
                InvitedByMemberId = me.Value,
                CreatedAt = now,
                ExpiresAt = now.Add(InvitesEndpoints.InviteLifetime)
            };

            var delivered = true;
            if (channel == "sms")
            {
                if (!PhoneHelpers.TryNormalize(req.Phone, out var e164))
                    return Results.Problem("Ogiltigt telefonnummer", statusCode: 400);
                invite.Channel = InviteChannel.Sms;
                invite.PhoneNumber = e164;
                db.Invites.Add(invite);
                await db.SaveChangesAsync(ct);
                try { await sms.SendInviteSmsAsync(e164, inviter.Name, householdName, acceptUrl, ct); }
                catch (InvalidOperationException) { delivered = false; }
            }
            else
            {
                var email = (req.Email ?? "").Trim().ToLowerInvariant();
                if (!AccountHelpers.IsValidEmail(email))
                    return Results.Problem("Ogiltig e-postadress", statusCode: 400);
                invite.Channel = InviteChannel.Email;
                invite.Email = email;
                db.Invites.Add(invite);
                await db.SaveChangesAsync(ct);
                try
                {
                    if (householdName is not null)
                        await emailSender.SendInviteEmailAsync(email, householdName, inviter.Name, acceptUrl, ct);
                    else
                        await emailSender.SendContactInviteEmailAsync(email, inviter.Name, acceptUrl, ct);
                }
                catch (InvalidOperationException) { delivered = false; }
            }

            // Always the same shape — the response never signals whether the recipient exists.
            return Results.Created($"/contacts/invites/{invite.Id}",
                new ContactInviteResultDto(invite.Id, Contract.InviteChannel(invite.Channel), invite.ExpiresAt, delivered));
        }).WithName("CreateContactInvite")
            .RequireRateLimiting(InviteRateLimitPolicy)
            .Produces<ContactInviteResultDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("/households/{id:guid}/invite-contact", async (
            Guid id, InviteContactRequest req, ICurrentUserAccessor cu, SettlDbContext db,
            IEmailSender emailSender, IConfiguration config, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            // Must be an actual saved contact of the acting member (proves prior consent) — never
            // an arbitrary member id, so this can't be used to enumerate or spam strangers.
            var isContact = await db.Contacts.AnyAsync(c => c.OwnerMemberId == me && c.ContactMemberId == req.ContactMemberId, ct);
            if (!isContact) return Results.Problem("Personen finns inte bland dina kontakter", statusCode: 404);

            var contact = await db.Members.FirstOrDefaultAsync(m => m.Id == req.ContactMemberId, ct);
            if (contact?.Email is null) return Results.Problem("Kontakten saknar e-post", statusCode: 400);

            var already = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id && m.MemberId == req.ContactMemberId, ct);
            if (already) return Results.Problem("Personen är redan medlem", statusCode: 409);

            var household = await db.Households.FirstAsync(h => h.Id == id, ct);
            var inviter = await db.Members.FirstAsync(m => m.Id == me, ct);
            var now = DateTimeOffset.UtcNow;
            var rawToken = InviteTokens.NewRawToken();
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                HouseholdId = id,
                Channel = InviteChannel.Email,
                Email = contact.Email.ToLowerInvariant(),
                TokenHash = InviteTokens.Hash(rawToken),
                InvitedByMemberId = me.Value,
                CreatedAt = now,
                ExpiresAt = now.Add(InvitesEndpoints.InviteLifetime)
            };
            db.Invites.Add(invite);
            await db.SaveChangesAsync(ct);

            var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
            var acceptUrl = $"{baseUrl}/accept-invite?token={rawToken}";
            var delivered = true;
            try { await emailSender.SendInviteEmailAsync(contact.Email, household.Name, inviter.Name, acceptUrl, ct); }
            catch (InvalidOperationException) { delivered = false; }

            return Results.Created($"/households/{id}/invites/{invite.Id}",
                new ContactInviteResultDto(invite.Id, "email", invite.ExpiresAt, delivered));
        }).WithName("InviteContactToHousehold")
            .Produces<ContactInviteResultDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/households/{id:guid}/invitable-contacts", async (
            Guid id, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var isMember = await db.HouseholdMemberships.AnyAsync(m => m.HouseholdId == id && m.MemberId == me, ct);
            if (!isMember) return Results.Problem("Du är inte medlem i hushållet", statusCode: 403);

            var contacts = await db.Contacts
                .Where(c => c.OwnerMemberId == me)
                .Include(c => c.ContactMember)
                .ToListAsync(ct);

            var memberIds = (await db.HouseholdMemberships
                .Where(m => m.HouseholdId == id)
                .Select(m => m.MemberId)
                .ToListAsync(ct)).ToHashSet();

            // Emails with an outstanding (unexpired, unaccepted) invite to THIS household.
            var now = DateTimeOffset.UtcNow;
            var pendingEmails = (await db.Invites
                    .Where(i => i.HouseholdId == id && i.AcceptedAt == null && i.Email != null)
                    .ToListAsync(ct))
                .Where(i => i.ExpiresAt > now)
                .Select(i => i.Email!.ToLowerInvariant())
                .ToHashSet();

            var result = contacts
                .Select(c =>
                {
                    var status = memberIds.Contains(c.ContactMemberId) ? "member"
                        : c.ContactMember.Email is { } e && pendingEmails.Contains(e.ToLowerInvariant()) ? "pending"
                        : "invitable";
                    return new InvitableContactDto(c.ContactMemberId, c.ContactMember.Name, c.ContactMember.AvatarColor, status);
                })
                .OrderBy(r => r.Name)
                .ToList();

            return Results.Ok(result);
        }).WithName("GetInvitableContacts")
            .Produces<List<InvitableContactDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
