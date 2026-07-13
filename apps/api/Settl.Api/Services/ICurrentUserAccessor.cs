using System.Security.Claims;

namespace Settl.Api.Services;

/// <summary>
/// Resolves the acting member from the authenticated principal (ADR-0011). This is the
/// ONLY place "who am I" is decided — every endpoint file calls this instead of reading
/// auth state itself.
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>The acting member's id. Null if unauthenticated (the fallback authorization
    /// policy means this only happens for AllowAnonymous endpoints).</summary>
    Task<Guid?> GetMemberIdAsync(CancellationToken ct = default);
}

public sealed class CurrentUserAccessor(IHttpContextAccessor http) : ICurrentUserAccessor
{
    public Task<Guid?> GetMemberIdAsync(CancellationToken ct = default)
    {
        var id = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Task.FromResult(Guid.TryParse(id, out var memberId) ? memberId : (Guid?)null);
    }
}
