using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder app)
    {
        static MeDto ToMeDto(Domain.Member m) =>
            new(m.Id, m.Name, m.AvatarColor, m.AvatarEmoji, m.Email, m.EmailConfirmed);

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
                : Results.Ok(ToMeDto(m));
        }).WithName("GetCurrentUser")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Update the acting member's own name + avatar emoji (ADR-0019). AvatarColor and email
        // are not editable here. "AuthenticatedOnly" (not the confirmed-email fallback) so an
        // unconfirmed member can still fix their name/avatar — same reasoning as GET /me.
        app.MapPut("/me", async (
            UpdateMeRequest req, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var id = await cu.GetMemberIdAsync(ct);
            if (id is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            var name = req.Name?.Trim() ?? "";
            if (name.Length == 0) return Results.Problem("Ange ditt namn", statusCode: StatusCodes.Status400BadRequest);

            // Untrusted: the emoji renders in other members' UIs, so the API decides what is
            // storable (ADR-0006/0019), never the client picker. null/empty => reset to initial.
            if (!AccountHelpers.TryNormalizeAvatarEmoji(req.AvatarEmoji, out var emoji, out var emojiError))
                return Results.Problem(emojiError ?? "Ogiltig emoji", statusCode: StatusCodes.Status400BadRequest);

            var m = await db.Members.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (m is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            m.Name = name;
            m.AvatarEmoji = emoji;
            await db.SaveChangesAsync(ct);

            return Results.Ok(ToMeDto(m));
        }).WithName("UpdateCurrentUser")
            .RequireAuthorization("AuthenticatedOnly")
            .Produces<MeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
