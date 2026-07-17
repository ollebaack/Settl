using System.Collections.Concurrent;

namespace Settl.Api.Services;

/// <summary>
/// Holds the most recently generated link for each transactional email kind, in memory
/// only — raw tokens are never persisted (only their hash, for invites) or otherwise
/// recoverable. Lets local dev/Playwright read the link a real inbox would have received
/// (the GET /dev/... endpoints in each feature file, Development-only). Populated by
/// <see cref="DevEmailSender"/>, so it stays empty whenever Resend is configured.
///
/// Every link kind is ALSO indexed by recipient (email / phone) so parallel Playwright workers
/// can each read their OWN link instead of racing on the single most-recent slot, where a
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
    private readonly ConcurrentDictionary<string, string> _verificationByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _passwordResetByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _smsInviteByPhone = new();

    // Nudge-digest sends, per recipient: the unsubscribe link and how many digests were sent.
    // Lets integration tests assert "emailed once" and read the tokenised unsubscribe URL a real
    // inbox would have received (reminder-delivery spec).
    private readonly ConcurrentDictionary<string, (string UnsubscribeUrl, int NudgeCount, int SendCount)> _digestByEmail
        = new(StringComparer.OrdinalIgnoreCase);

    public void RecordInviteAccept(string email, string url)
    {
        _lastInviteAcceptUrl = url;
        _inviteByEmail[email.Trim()] = url;
    }

    public void RecordVerification(string email, string url)
    {
        _lastVerificationUrl = url;
        _verificationByEmail[email.Trim()] = url;
    }

    public void RecordPasswordReset(string email, string url)
    {
        _lastPasswordResetUrl = url;
        _passwordResetByEmail[email.Trim()] = url;
    }

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

    /// <summary>The verification link for a specific account email, or null if none recorded.</summary>
    public string? VerificationUrlFor(string email) =>
        _verificationByEmail.TryGetValue(email.Trim(), out var url) ? url : null;

    /// <summary>The password-reset link for a specific account email, or null if none recorded.</summary>
    public string? PasswordResetUrlFor(string email) =>
        _passwordResetByEmail.TryGetValue(email.Trim(), out var url) ? url : null;

    /// <summary>The accept link for a specific invitee phone (E.164), or null if none recorded.</summary>
    public string? SmsInviteAcceptUrlFor(string phoneE164) =>
        _smsInviteByPhone.TryGetValue(phoneE164.Trim(), out var url) ? url : null;

    /// <summary>Records a nudge-digest send: keeps the latest unsubscribe URL / nudge count and
    /// bumps the per-recipient send counter (so a de-duplicated second pass shows no new send).</summary>
    public void RecordNudgeDigest(string email, string unsubscribeUrl, int nudgeCount) =>
        _digestByEmail.AddOrUpdate(
            email.Trim(),
            (unsubscribeUrl, nudgeCount, 1),
            (_, prev) => (unsubscribeUrl, nudgeCount, prev.SendCount + 1));

    /// <summary>The most recent digest recorded for a recipient, or null if none was sent.</summary>
    public (string UnsubscribeUrl, int NudgeCount, int SendCount)? NudgeDigestFor(string email) =>
        _digestByEmail.TryGetValue(email.Trim(), out var v) ? v : null;
}
