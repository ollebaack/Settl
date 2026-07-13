namespace Settl.Api.Services;

/// <summary>
/// Holds the most recently generated invite accept link, in memory only — the raw token
/// is never persisted (only its hash is, on <c>Invite</c>). Lets local dev/Playwright read
/// the link a real inbox would have received (GET /dev/invites/latest, Development-only).
/// Populated by <see cref="DevEmailSender"/>, so it's empty whenever Resend is configured.
/// </summary>
public sealed class DevInviteLinkStore
{
    private volatile string? _lastAcceptUrl;

    public void Record(string acceptUrl) => _lastAcceptUrl = acceptUrl;

    public string? LastAcceptUrl => _lastAcceptUrl;
}
