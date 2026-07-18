namespace Settl.Api.Services;

/// <summary>
/// Sends transactional SMS (contacts-phone-sms spec: blind contact invites). No vendor is wired yet — the
/// spec defers the Sinch/Vonage/Twilio choice, mirroring how ADR-0005 chose email delivery
/// ahead of any sending code. Only <see cref="DevSmsSender"/> exists today; a real provider
/// is registered the same way <see cref="ResendEmailSender"/> is once picked. Rate-limiting
/// ships WITH this channel (see Program.cs) because each SMS costs money — SMS pumping is a
/// documented fraud vector, unlike near-free email (tech-debt/0006).
/// </summary>
public interface ISmsSender
{
    /// <param name="householdName">The household the invitee is being added to, or null for a
    /// contact-only invite.</param>
    Task SendInviteSmsAsync(string toPhoneE164, string inviterName, string? householdName, string acceptUrl, CancellationToken ct = default);
}

/// <summary>
/// Logs the invite link instead of sending a real text — the SMS equivalent of
/// <see cref="DevSmsSender"/>'s email counterpart. Registered until a real SMS vendor is wired
/// (the contacts-phone-sms spec defers that choice), so contact-invite flows work end-to-end in dev/e2e without a
/// paid vendor. Playwright reads the link back via the GET /dev/sms-invites/latest side channel.
/// </summary>
public sealed class DevSmsSender(ILogger<DevSmsSender> logger, DevEmailLinkStore linkStore) : ISmsSender
{
    public Task SendInviteSmsAsync(string toPhoneE164, string inviterName, string? householdName, string acceptUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev sms] Invite for {ToPhone} from {InviterName} ({Household}): {AcceptUrl}",
            toPhoneE164, inviterName, householdName ?? "kontakt", acceptUrl);
        linkStore.RecordSmsInvite(toPhoneE164, acceptUrl);
        return Task.CompletedTask;
    }
}
