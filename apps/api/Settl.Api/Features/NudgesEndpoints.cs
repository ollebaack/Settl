using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class NudgesEndpoints
{
    public static IEndpointRouteBuilder MapNudgesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/households/{id:guid}/nudges", async (
            Guid id, string? tone, ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var me = await cu.GetMemberIdAsync(ct);
            if (me is null) return Results.Problem("Ingen användare", statusCode: 404);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var emittable = await NudgeComputation.ForMember(db, id, me.Value, tone ?? "direct", today, ct);
            if (emittable is null) return Results.Problem("Hushållet hittades inte", statusCode: 404);

            // The read path returns the nudges only; the identity keys back the delivery dedup.
            return Results.Ok(emittable.Select(e => e.Nudge).ToList());
        }).WithName("GetNudges")
            .Produces<List<NudgeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
