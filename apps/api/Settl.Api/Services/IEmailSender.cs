using System.Net.Http.Json;

namespace Settl.Api.Services;

/// <summary>Sends transactional email (ADR-0011: invites, verification, password reset).</summary>
public interface IEmailSender
{
    Task SendInviteEmailAsync(string toEmail, string householdName, string inviterName, string acceptUrl, CancellationToken ct = default);

    /// <summary>Contact-only invite (ADR-0019): no household to join, just an invitation to
    /// connect on Settl. Used when the email channel is picked from the contacts tab.</summary>
    Task SendContactInviteEmailAsync(string toEmail, string inviterName, string acceptUrl, CancellationToken ct = default);

    Task SendVerificationEmailAsync(string toEmail, string confirmUrl, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct = default);

    /// <summary>The daily nudge digest (reminder-delivery spec, ADR-0024). <paramref name="lines"/>
    /// is the member's un-sent nudges, tone already baked into the copy; <paramref name="unsubscribeUrl"/>
    /// is the tokenised one-click opt-out reachable without login.</summary>
    Task SendNudgeDigestEmailAsync(
        string toEmail, string memberName, IReadOnlyList<NudgeDigestLine> lines, string unsubscribeUrl,
        CancellationToken ct = default);
}

/// <summary>One nudge as it appears in the digest email — the render-facing subset of a nudge,
/// so <see cref="IEmailSender"/> doesn't depend on the API DTOs.</summary>
public sealed record NudgeDigestLine(string Title, string Body, string When);

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

    public Task SendContactInviteEmailAsync(string toEmail, string inviterName, string acceptUrl, CancellationToken ct = default) =>
        SendAsync(toEmail, $"{inviterName} vill lägga till dig som kontakt på Settl", $"""
            <p>{inviterName} vill lägga till dig som kontakt på Settl.</p>
            <p><a href="{acceptUrl}">Acceptera</a></p>
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

    public Task SendNudgeDigestEmailAsync(
        string toEmail, string memberName, IReadOnlyList<NudgeDigestLine> lines, string unsubscribeUrl,
        CancellationToken ct = default) =>
        SendAsync(toEmail, NudgeDigestEmail.Subject(lines.Count),
            NudgeDigestEmail.Html(memberName, lines, unsubscribeUrl), "Kunde inte skicka påminnelse", ct);

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
        linkStore.RecordInviteAccept(toEmail, acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendContactInviteEmailAsync(string toEmail, string inviterName, string acceptUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Contact invite for {ToEmail} from {InviterName}: {AcceptUrl}",
            toEmail, inviterName, acceptUrl);
        linkStore.RecordInviteAccept(toEmail, acceptUrl);
        return Task.CompletedTask;
    }

    public Task SendVerificationEmailAsync(string toEmail, string confirmUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Verification for {ToEmail}: {ConfirmUrl}", toEmail, confirmUrl);
        linkStore.RecordVerification(toEmail, confirmUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetUrl, CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Password reset for {ToEmail}: {ResetUrl}", toEmail, resetUrl);
        linkStore.RecordPasswordReset(toEmail, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendNudgeDigestEmailAsync(
        string toEmail, string memberName, IReadOnlyList<NudgeDigestLine> lines, string unsubscribeUrl,
        CancellationToken ct = default)
    {
        logger.LogInformation("[dev email] Nudge digest for {ToEmail}: {Count} nudge(s), unsubscribe {UnsubscribeUrl}",
            toEmail, lines.Count, unsubscribeUrl);
        linkStore.RecordNudgeDigest(toEmail, unsubscribeUrl, lines.Count);
        return Task.CompletedTask;
    }
}

/// <summary>Renders the daily digest email — shared by the real and dev senders so the copy is
/// defined once. Plain, inline-styled HTML (email clients strip stylesheets).</summary>
public static class NudgeDigestEmail
{
    public static string Subject(int count) =>
        count == 1 ? "Du har en påminnelse på Settl" : $"Du har {count} påminnelser på Settl";

    public static string Html(string memberName, IReadOnlyList<NudgeDigestLine> lines, string unsubscribeUrl)
    {
        var items = string.Join("\n", lines.Select(l => $"""
            <li style="margin-bottom:12px">
              <strong>{Encode(l.Title)}</strong><br/>
              <span>{Encode(l.Body)}</span>
              <span style="color:#888"> · {Encode(l.When)}</span>
            </li>
            """));

        return $"""
            <p>Hej {Encode(memberName)},</p>
            <p>Här är dagens sammanfattning från Settl:</p>
            <ul style="padding-left:18px">
            {items}
            </ul>
            <p style="color:#888;font-size:12px;margin-top:24px">
              Du får det här mejlet eftersom du har påminnelser aktiverade.
              <a href="{unsubscribeUrl}">Sluta få påminnelser via e-post</a>.
            </p>
            """;
    }

    // The nudge copy is app-generated, but titles carry user data (household/expense names), so
    // HTML-encode every field before it lands in the markup.
    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
