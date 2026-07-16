using System.Security.Cryptography;
using System.Text;

namespace Settl.Api.Services;

/// <summary>
/// Invite token minting/hashing (ADR-0011/0019). The raw token lives only in the emailed or
/// texted accept link; only its SHA-256 hash is persisted, so a leaked DB never yields a
/// usable link. Shared by the email (household) and SMS/contact invite paths.
/// </summary>
public static class InviteTokens
{
    public static string NewRawToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
