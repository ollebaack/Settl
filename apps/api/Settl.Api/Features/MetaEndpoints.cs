using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder app)
    {
        // "AuthenticatedOnly", not the fallback policy: an unconfirmed member must still be
        // able to read their own EmailConfirmed status, so the web app knows to show
        // /verify-email instead of treating this like every other (confirmed-only) endpoint.
        app.MapGet("/me", async (ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var id = await cu.GetMemberIdAsync(ct);
            if (id is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            var m = await db.Members.FirstOrDefaultAsync(x => x.Id == id, ct);
            return m is null
                ? Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(m.ToMeDto());
        }).WithName("GetCurrentUser")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Update the acting member's own name, avatar emoji, nudge prefs and phone number
        // (contacts-phone-sms spec — one number, also the Swish payee). AvatarColor and email are not editable
        // here. "AuthenticatedOnly" (not the confirmed-email fallback) so an unconfirmed member can
        // still fix their name/avatar/phone before confirming their email — same reasoning as GET /me.
        app.MapPut("/me", async (
            UpdateMeRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var id = await cu.GetMemberIdAsync(ct);
            if (id is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            var name = req.Name?.Trim() ?? "";
            if (name.Length == 0) return Results.Problem("Ange ditt namn", statusCode: StatusCodes.Status400BadRequest);

            // Untrusted: the emoji renders in other members' UIs, so the API decides what is
            // storable (ADR-0006, contacts-phone-sms spec), never the client picker. null/empty => reset to initial.
            if (!AccountHelpers.TryNormalizeAvatarEmoji(req.AvatarEmoji, out var emoji, out var emojiError))
                return Results.Problem(emojiError ?? "Ogiltig emoji", statusCode: StatusCodes.Status400BadRequest);

            // Nudge tone (implementation-map §2.4): null leaves it unchanged; a present value must
            // be a known tone — the API is authoritative over the enum, not the client (ADR-0006).
            Domain.NudgeTone? tone = null;
            if (req.NudgeTone is not null)
            {
                tone = Contract.TryParseNudgeTone(req.NudgeTone);
                if (tone is null) return Results.Problem("Ogiltig ton", statusCode: StatusCodes.Status400BadRequest);
            }

            var m = await db.Members.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            // The member's single phone number (contacts-phone-sms spec): normalised to E.164 and stored UNVERIFIED
            // (tech-debt/0010) — display/contact data only, never a lookup key or auth factor. It
            // also powers "Betala med Swish". The API is authoritative over the format (ADR-0006).
            // Unlike the nudge toggles above, the profile form always submits this field, so
            // null/empty CLEARS it rather than leaving it unchanged.
            string? phone;
            if (string.IsNullOrWhiteSpace(req.Phone))
            {
                phone = null;
            }
            else if (PhoneHelpers.TryNormalize(req.Phone, out var e164))
            {
                phone = e164;
            }
            else
            {
                return Results.Problem("Ogiltigt telefonnummer", statusCode: StatusCodes.Status400BadRequest);
            }

            m.Name = name;
            m.AvatarEmoji = emoji;
            if (tone is not null) m.NudgeTone = tone.Value;
            // Nudge-email opt-in (reminder-delivery spec): null leaves it unchanged, so a name/emoji
            // edit never flips the preference. The same flag is toggled login-free via unsubscribe.
            if (req.NudgeEmailsEnabled is { } enabled) m.NudgeEmailsEnabled = enabled;
            m.PhoneNumber = phone;
            // Never trust an unverified number: keep it unconfirmed until an OTP lands (tech-debt/0010).
            m.PhoneNumberConfirmed = false;
            await db.SaveChangesAsync(ct);

            return Results.Ok(m.ToMeDto());
        }).WithName("UpdateCurrentUser")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
