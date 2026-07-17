using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Services;

namespace Settl.Api.Features;

/// <summary>
/// The login-free nudge-email unsubscribe reached from a digest (reminder-delivery spec, ADR-0024).
/// The link carries a Data-Protection-sealed member token (<see cref="NudgeUnsubscribeTokens"/>), so
/// no session is needed to turn emails OFF. Two steps by design: the email link is a GET that shows
/// a one-button confirmation page, and the button POSTs the actual change — so an email client
/// prefetching the GET can't silently unsubscribe anyone. Anonymous, and it only ever turns the
/// preference off; turning it back on requires the authenticated profile (PUT /me).
/// </summary>
public static class UnsubscribeEndpoints
{
    public static IEndpointRouteBuilder MapUnsubscribeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/nudges/unsubscribe", (string? token, NudgeUnsubscribeTokens tokens) =>
        {
            if (!tokens.TryValidate(token, out _))
                return Results.Content(Page("Länken är ogiltig eller har gått ut.", form: null), "text/html; charset=utf-8");

            // Valid token → render the confirm form (POSTs the same token back).
            var safeToken = System.Net.WebUtility.HtmlEncode(token);
            return Results.Content(Page(
                "Vill du sluta få påminnelser via e-post från Settl?",
                form: $"""
                    <form method="post" action="/nudges/unsubscribe">
                      <input type="hidden" name="token" value="{safeToken}" />
                      <button type="submit">Sluta få påminnelser</button>
                    </form>
                    """), "text/html; charset=utf-8");
        }).WithName("UnsubscribeNudgesPage")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK);

        app.MapPost("/nudges/unsubscribe", async (
            HttpRequest req, NudgeUnsubscribeTokens tokens, SettlDbContext db, CancellationToken ct) =>
        {
            var token = req.HasFormContentType ? (await req.ReadFormAsync(ct))["token"].ToString() : null;
            if (string.IsNullOrEmpty(token)) token = req.Query["token"].ToString();

            if (!tokens.TryValidate(token, out var memberId))
                return Results.Problem("Länken är ogiltig eller har gått ut", statusCode: StatusCodes.Status400BadRequest);

            var member = await db.Members.FirstOrDefaultAsync(m => m.Id == memberId, ct);
            // Idempotent: an unknown/already-off member still reports success — the goal state holds.
            if (member is not null && member.NudgeEmailsEnabled)
            {
                member.NudgeEmailsEnabled = false;
                await db.SaveChangesAsync(ct);
            }

            return Results.Content(
                Page("Klart — du får inte längre påminnelser via e-post. Du kan slå på dem igen under Profil.", form: null),
                "text/html; charset=utf-8");
        }).WithName("UnsubscribeNudges")
            .AllowAnonymous()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static string Page(string message, string? form) => $"""
        <!doctype html>
        <html lang="sv">
        <head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Settl — påminnelser</title></head>
        <body style="font-family:system-ui,sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem">
          <h1 style="font-size:1.25rem">Settl</h1>
          <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
          {form ?? ""}
        </body>
        </html>
        """;
}
