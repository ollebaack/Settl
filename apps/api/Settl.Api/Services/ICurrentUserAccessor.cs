using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;

namespace Settl.Api.Services;

/// <summary>
/// Resolves the acting member. Auth is deferred (ADR-0005); this is the ONLY place
/// "who am I" is decided. Reads header <c>X-Settl-User: {memberId}</c> when present and
/// valid, else falls back to a configured default (the first seeded member, "Du").
/// Tech-debt: replace with real auth (docs/tech-debt/0003).
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>The acting member's id, resolving header → default. Null only before any member exists.</summary>
    Task<Guid?> GetMemberIdAsync(CancellationToken ct = default);
}

public sealed class CurrentUserAccessor(IHttpContextAccessor http, SettlDbContext db) : ICurrentUserAccessor
{
    public const string HeaderName = "X-Settl-User";

    public async Task<Guid?> GetMemberIdAsync(CancellationToken ct = default)
    {
        var header = http.HttpContext?.Request.Headers[HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(header) && Guid.TryParse(header, out var id))
        {
            if (await db.Members.AnyAsync(m => m.Id == id, ct))
                return id;
        }

        // Default = seeded member "Du", else earliest member by id (deterministic).
        var du = await db.Members
            .Where(m => m.Name == "Du")
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
        if (du is not null) return du;

        return await db.Members
            .OrderBy(m => m.Id)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);
    }
}
