using System.Collections.Concurrent;

namespace Settl.Api.Services;

/// <summary>
/// Holds the most recently generated link for each transactional email kind, in memory
/// only — raw tokens are never persisted (only their hash, for invites) or otherwise
/// recoverable. Lets local dev/Playwright read the link a real inbox would have received
/// (the GET /dev/... endpoints in each feature file, Development-only). Populated by
/// <see cref="DevEmailSender"/>, so it stays empty whenever Resend is configured.
///
/// Invite links are ALSO indexed by recipient (email / phone) so parallel Playwright workers
/// can each read their OWN invite instead of racing on the single most-recent slot, where a
/// competing signup evicts a spec's link before it reads it. The unkeyed <c>Last*</c> values
/// still back the no-argument dev endpoints (local manual dev).
/// </summary>
public sealed class DevEmailLinkStore
{
    private volatile string? _lastInviteAcceptUrl;
    private volatile string? _lastVerificationUrl;
    private volatile string? _lastPasswordResetUrl;
    private volatile string? _lastSmsInviteAcceptUrl;

    private readonly ConcurrentDictionary<string, string> _inviteByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _smsInviteByPhone = new();

    public void RecordInviteAccept(string email, string url)
    {
        _lastInviteAcceptUrl = url;
        _inviteByEmail[email.Trim()] = url;
    }

    public void RecordVerification(string url) => _lastVerificationUrl = url;
    public void RecordPasswordReset(string url) => _lastPasswordResetUrl = url;

    public void RecordSmsInvite(string phoneE164, string url)
    {
        _lastSmsInviteAcceptUrl = url;
        _smsInviteByPhone[phoneE164.Trim()] = url;
    }

    public string? LastInviteAcceptUrl => _lastInviteAcceptUrl;
    public string? LastVerificationUrl => _lastVerificationUrl;
    public string? LastPasswordResetUrl => _lastPasswordResetUrl;
    public string? LastSmsInviteAcceptUrl => _lastSmsInviteAcceptUrl;

    /// <summary>The accept link for a specific invitee email, or null if none recorded.</summary>
    public string? InviteAcceptUrlFor(string email) =>
        _inviteByEmail.TryGetValue(email.Trim(), out var url) ? url : null;

    /// <summary>The accept link for a specific invitee phone (E.164), or null if none recorded.</summary>
    public string? SmsInviteAcceptUrlFor(string phoneE164) =>
        _smsInviteByPhone.TryGetValue(phoneE164.Trim(), out var url) ? url : null;
}
