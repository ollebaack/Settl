using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Dtos;
using Settl.Api.Services;

namespace Settl.Api.Features;

public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (ICurrentUserAccessor cu, SettlDbContext db, CancellationToken ct) =>
        {
            var id = await cu.GetMemberIdAsync(ct);
            if (id is null) return Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound);

            var m = await db.Members.FirstOrDefaultAsync(x => x.Id == id, ct);
            return m is null
                ? Results.Problem("Ingen användare hittades", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(new MemberDto(m.Id, m.Name, m.AvatarColor));
        }).WithName("GetCurrentUser")
            .Produces<MemberDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
