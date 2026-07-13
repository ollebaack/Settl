namespace Settl.Api.Services;

/// <summary>
/// Holds the most recently generated link for each transactional email kind, in memory
/// only — raw tokens are never persisted (only their hash, for invites) or otherwise
/// recoverable. Lets local dev/Playwright read the link a real inbox would have received
/// (the GET /dev/... endpoints in each feature file, Development-only). Populated by
/// <see cref="DevEmailSender"/>, so it stays empty whenever Resend is configured.
/// </summary>
public sealed class DevEmailLinkStore
{
    private volatile string? _lastInviteAcceptUrl;
    private volatile string? _lastVerificationUrl;
    private volatile string? _lastPasswordResetUrl;

    public void RecordInviteAccept(string url) => _lastInviteAcceptUrl = url;
    public void RecordVerification(string url) => _lastVerificationUrl = url;
    public void RecordPasswordReset(string url) => _lastPasswordResetUrl = url;

    public string? LastInviteAcceptUrl => _lastInviteAcceptUrl;
    public string? LastVerificationUrl => _lastVerificationUrl;
    public string? LastPasswordResetUrl => _lastPasswordResetUrl;
}
