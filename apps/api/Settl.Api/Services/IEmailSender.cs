using System.Net.Http.Json;

namespace Settl.Api.Services;

/// <summary>Sends transactional email (ADR-0011: invites, verification, password reset).</summary>
public interface IEmailSender
{
    Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default);
    Task SendVerificationEmailAsync(string toEmail, string confirmUrl, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct = default);
}

/// <summary>
/// Sends via Resend's HTTP API directly (one JSON POST) — no Resend SDK package, since
/// hand-rolling this single call avoids a second new dependency for the same vendor
/// decision ADR-0011 already made.
/// </summary>
public sealed class ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, $"{inviterName} bjöd in dig till {householdName} på Settl", $"""
            <p>{inviterName} har bjudit in dig till hushållet <strong>{householdName}</strong> på Settl.</p>
            <p><a href="{acceptUrl}">Acceptera inbjudan</a></p>
            <p>Länken slutar gälla om 7 dagar.</p>
            """, "Kunde inte skicka inbjudan", ct);

    public Task SendVerificationEmailAsync(string toEmail, string confirmUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, "Bekräfta din e-postadress på Settl", $"""
            <p>Klicka på länken för att bekräfta din e-postadress och komma igång med Settl.</p>
            <p><a href="{confirmUrl}">Bekräfta e-postadress</a></p>
            """, "Kunde inte skicka bekräftelsemejl", ct);

    public Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, "Återställ ditt lösenord på Settl", $"""
            <p>Klicka på länken för att välja ett nytt lösenord.</p>
            <p><a href="{resetUrl}">Återställ lösenord</a></p>
            <p>Länken slutar gälla om 1 timme. Om du inte bad om detta kan du ignorera mejlet.</p>
            """, "Kunde inte skicka återställningsmejl", ct);

    private async Task SendAsync(string toEmail, string subject, string html, string failureMessage, CancellationToken ct)
    {
        var from = config["Resend:FromAddress"] ?? "Settl <no-reply@settlapp.se>";
        var body = new { from, to = new[] { toEmail }, subject, html };

        var response = await http.PostAsJsonAsync("emails", body, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend send failed ({Status}): {Detail}", response.StatusCode, detail);
            throw new InvalidOperationException(failureMessage);
        }
    }
}

/// <summary>
/// Logs the link instead of sending. Registered whenever no Resend API key is configured —
/// always true in local dev, so auth flows work without a real inbox (Playwright reads the
/// links back via the GET /dev/... side channels below).
/// </summary>
public sealed class DevEmailSender(ILogger<DevEmailSender> logger, DevEmailLinkStore linkStore) : IEmailSender
{
    public Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Invite for {ToEmail} to {HouseholdName} from {InviterName}: {AcceptUrl}",
            toEmail, householdName, inviterName, acceptUrl);
        linkStore.RecordInviteAccept(acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendVerificationEmailAsync(string toEmail, string confirmUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Verification for {ToEmail}: {ConfirmUrl}", toEmail, confirmUrl);
        linkStore.RecordVerification(confirmUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Password reset for {ToEmail}: {ResetUrl}", toEmail, resetUrl);
        linkStore.RecordPasswordReset(resetUrl);
        return Task.CompletedTask;
    }
}
