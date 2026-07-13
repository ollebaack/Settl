using System.Net.Http.Json;

namespace Settl.Api.Services;

/// <summary>Sends transactional email (ADR-0011: invites, later password reset).</summary>
public interface IEmailSender
{
    Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default);
}

/// <summary>
/// Sends via Resend's HTTP API directly (one JSON POST) — no Resend SDK package, since
/// hand-rolling this single call avoids a second new dependency for the same vendor
/// decision ADR-0011 already made.
/// </summary>
public sealed class ResendEmailSender(HttpClient http, IConfiguration config, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default)
    {
        var from = config["Resend:FromAddress"] ?? "Settl <no-reply@settl.dev>";
        var body = new
        {
            from,
            to = new[] { toEmail },
            subject = $"{inviterName} bjöd in dig till {householdName} på Settl",
            html = $"""
                <p>{inviterName} har bjudit in dig till hushållet <strong>{householdName}</strong> på Settl.</p>
                <p><a href="{acceptUrl}">Acceptera inbjudan</a></p>
                <p>Länken slutar gälla om 7 dagar.</p>
                """
        };

        var response = await http.PostAsJsonAsync("emails", body, ct);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Resend send failed ({Status}): {Detail}", response.StatusCode, detail);
            throw new InvalidOperationException("Kunde inte skicka inbjudan");
        }
    }
}

/// <summary>
/// Logs the accept link instead of sending. Registered whenever no Resend API key is
/// configured — always true in local dev, so invites work without a real inbox
/// (Playwright reads the link back via GET /dev/invites/latest).
/// </summary>
public sealed class DevEmailSender(ILogger<DevEmailSender> logger, DevInviteLinkStore linkStore) : IEmailSender
{
    public Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Invite for {ToEmail} to {HouseholdName} from {InviterName}: {AcceptUrl}",
            toEmail, householdName, inviterName, acceptUrl);
        linkStore.Record(acceptUrl);
        return Task.CompletedTask;
    }
}
