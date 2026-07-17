using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Settl.Api.Services;

/// <summary>
/// Mints and validates the tokenised, login-free unsubscribe link carried in every nudge digest
/// (reminder-delivery spec). The token is the member id sealed with ASP.NET Data Protection under
/// a dedicated purpose — stateless (no stored token column), tamper-proof, and using the same key
/// ring as the app's other tokens (persisted in prod, ADR-0011/Program.cs). No expiry: an
/// unsubscribe link should keep working, and the action is trivially reversible in-app.
/// </summary>
public sealed class NudgeUnsubscribeTokens(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Settl.NudgeUnsubscribe.v1");

    public string Create(Guid memberId) => _protector.Protect(memberId.ToString());

    /// <summary>Unseals the token to a member id. Returns false for a missing, malformed, or
    /// tampered token — the endpoint turns that into a neutral response, never a 500.</summary>
    public bool TryValidate(string? token, out Guid memberId)
    {
        memberId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try { return Guid.TryParse(_protector.Unprotect(token), out memberId); }
        catch (CryptographicException) { return false; }
    }
}
