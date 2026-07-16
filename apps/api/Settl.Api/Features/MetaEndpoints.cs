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

        // Update the acting member's own profile. Today only the optional phone (ADR-0019),
        // stored UNVERIFIED (no OTP — tech-debt/0010): display/contact data only, never a
        // lookup key or auth factor. Email stays the sole identity (ADR-0005), so it is not
        // editable here. "AuthenticatedOnly" (not the confirmed-email fallback) so a member can
        // set their phone before confirming their email.
        app.MapPatch("/me", async (
            UpdateProfileRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var id = await cu.GetMemberIdAsync(ct);
            if (id is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            var m = await db.Members.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            if (string.IsNullOrWhiteSpace(req.Phone))
            {
                m.PhoneNumber = null;
            }
            else
            {
                if (!PhoneHelpers.TryNormalize(req.Phone, out var e164))
                    return Results.Problem("Ogiltigt telefonnummer", statusCode: StatusCodes.Status400BadRequest);
                m.PhoneNumber = e164;
            }
            // Never trust an unverified number: keep it unconfirmed until an OTP lands (tech-debt/0010).
            m.PhoneNumberConfirmed = false;
            await db.SaveChangesAsync(ct);

            return Results.Ok(m.ToMeDto());
        }).WithName("UpdateProfile")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
