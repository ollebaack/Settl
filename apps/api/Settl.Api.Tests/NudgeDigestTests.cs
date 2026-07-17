using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Settl.Api.Data;
using Settl.Api.Services;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Integration tests for the daily nudge-digest delivery (reminder-delivery spec, ADR-0024):
/// the emitted-nudge dedup, the once-a-day guard, and the login-free unsubscribe endpoint. Drives
/// the real <see cref="NudgeDigestService"/> against an isolated DB, reading what would have been
/// emailed from the dev email side channel (<see cref="DevEmailLinkStore"/>).
/// </summary>
public sealed class NudgeDigestTests
{
    private static DateTimeOffset Now() => DateTimeOffset.UtcNow;
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<int> RunDigest(SettlApiFactory factory, DateTimeOffset now, DateOnly today)
    {
        using var scope = factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<NudgeDigestService>();
        return await svc.RunAsync(now, today, CancellationToken.None);
    }

    private static DevEmailLinkStore Emails(SettlApiFactory factory) =>
        factory.Services.GetRequiredService<DevEmailLinkStore>();

    private static Task<string> EmailOf(SettlApiFactory factory, Guid memberId) =>
        factory.WithDb(db => db.Members.Where(m => m.Id == memberId).Select(m => m.Email!).SingleAsync());

    /// <summary>A household where member "A" owes a share of one big, unsettled expense — a single
    /// stable nudge, so day-to-day window shifts don't invent new nudges and dedup is testable.</summary>
    private static (TestScenario Scenario, Guid A) BigExpenseScenario()
    {
        var s = new TestScenario("Digesthus");
        // Both opted in — nudge emails are opt-in (default off), so delivery tests enable them.
        var a = s.AddMember("Alex", nudgeEmailsEnabled: true);
        var b = s.AddMember("Robin", nudgeEmailsEnabled: true);
        // 2000 kr, paid by B, equal split → A owes 1000 kr; ≥1500 kr amount → big-expense nudge.
        s.AddEqualExpense("Ny soffa", 200_000, paidBy: b, dateOffset: -1);
        return (s, a);
    }

    [Fact]
    public async Task Digest_sends_one_email_with_unsubscribe_link_to_member_with_a_nudge()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);

        var sent = await RunDigest(factory, Now(), Today());

        Assert.True(sent >= 1);
        var digest = Emails(factory).NudgeDigestFor(emailA);
        Assert.NotNull(digest);
        Assert.Equal(1, digest!.Value.SendCount);
        Assert.True(digest.Value.NudgeCount >= 1);
        Assert.Contains("/nudges/unsubscribe?token=", digest.Value.UnsubscribeUrl);
    }

    [Fact]
    public async Task Digest_is_silent_when_member_has_no_pending_nudges()
    {
        using var factory = new SettlApiFactory();
        var s = new TestScenario("Tysthus");
        // Opted in, so the only reason for silence is the absence of nudges — not the opt-out.
        var a = s.AddMember("Alex", nudgeEmailsEnabled: true);
        s.AddMember("Robin", nudgeEmailsEnabled: true);
        // No entries, no recurrings, square balances → nothing to say.
        await factory.SeedAsync(s);

        var sent = await RunDigest(factory, Now(), Today());

        Assert.Equal(0, sent);
        Assert.Null(Emails(factory).NudgeDigestFor(await EmailOf(factory, a)));
    }

    [Fact]
    public async Task Standing_nudge_is_emailed_once_across_days_dedup()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);

        // Day 1: sent. Day 3: the same conditions still stand (same identity keys) → NOT re-sent.
        await RunDigest(factory, Now(), Today());
        var rowsAfterDay1 = await factory.WithDb(db => db.EmittedNudges.CountAsync(en => en.MemberId == a));
        await RunDigest(factory, Now().AddDays(2), Today().AddDays(2));
        var rowsAfterDay3 = await factory.WithDb(db => db.EmittedNudges.CountAsync(en => en.MemberId == a));

        // Exactly one email to A (A has two standing nudges: the big expense and the balance),
        // and the second pass adds no new log rows — the standing conditions are not re-emailed.
        Assert.Equal(1, Emails(factory).NudgeDigestFor(emailA)!.Value.SendCount);
        Assert.True(rowsAfterDay1 >= 1);
        Assert.Equal(rowsAfterDay1, rowsAfterDay3);
    }

    [Fact]
    public async Task Second_pass_same_day_does_not_send_again()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);

        var now = Now();
        await RunDigest(factory, now, Today());
        var secondPass = await RunDigest(factory, now, Today());

        Assert.Equal(0, secondPass);
        Assert.Equal(1, Emails(factory).NudgeDigestFor(emailA)!.Value.SendCount);
    }

    [Fact]
    public async Task Opted_out_member_gets_no_digest()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);
        await factory.WithDb(async db =>
        {
            var m = await db.Members.SingleAsync(x => x.Id == a);
            m.NudgeEmailsEnabled = false;
            await db.SaveChangesAsync();
        });

        await RunDigest(factory, Now(), Today());

        // Per-member: A is opted out, so A gets nothing — even though A has pending nudges and the
        // other member (still opted in) is mailed.
        Assert.Null(Emails(factory).NudgeDigestFor(emailA));
        var rows = await factory.WithDb(db => db.EmittedNudges.CountAsync(en => en.MemberId == a));
        Assert.Equal(0, rows);
    }

    [Fact]
    public async Task Unconfirmed_email_gets_no_digest()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);
        await factory.WithDb(async db =>
        {
            var m = await db.Members.SingleAsync(x => x.Id == a);
            m.EmailConfirmed = false;
            await db.SaveChangesAsync();
        });

        await RunDigest(factory, Now(), Today());

        Assert.Null(Emails(factory).NudgeDigestFor(emailA));
    }

    // ------------------------------------------------------------------ Unsubscribe endpoint

    private static string TokenFromUrl(string url) => url[(url.IndexOf("token=", StringComparison.Ordinal) + 6)..];

    [Fact]
    public async Task Unsubscribe_post_turns_off_emails_without_login()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);

        await RunDigest(factory, Now(), Today());
        var token = TokenFromUrl(Emails(factory).NudgeDigestFor(emailA)!.Value.UnsubscribeUrl);

        // Anonymous client — no auth cookie.
        var anon = factory.CreateClient();
        var res = await anon.PostAsync($"/nudges/unsubscribe?token={token}", content: null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var enabled = await factory.WithDb(db => db.Members.Where(m => m.Id == a).Select(m => m.NudgeEmailsEnabled).SingleAsync());
        Assert.False(enabled);

        // A later pass (next day, so the once-a-day guard isn't what's stopping it) sends nothing.
        var sent = await RunDigest(factory, Now().AddDays(1), Today().AddDays(1));
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task Unsubscribe_get_renders_confirmation_form_for_valid_token()
    {
        using var factory = new SettlApiFactory();
        var (scenario, a) = BigExpenseScenario();
        await factory.SeedAsync(scenario);
        var emailA = await EmailOf(factory, a);

        await RunDigest(factory, Now(), Today());
        var token = TokenFromUrl(Emails(factory).NudgeDigestFor(emailA)!.Value.UnsubscribeUrl);

        var anon = factory.CreateClient();
        var res = await anon.GetAsync($"/nudges/unsubscribe?token={token}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        // The GET is a confirmation page that POSTs — it must NOT unsubscribe on its own (prefetch).
        Assert.Contains("method=\"post\"", html);
        Assert.Contains("/nudges/unsubscribe", html);
        var stillEnabled = await factory.WithDb(db => db.Members.Where(m => m.Id == a).Select(m => m.NudgeEmailsEnabled).SingleAsync());
        Assert.True(stillEnabled);
    }

    [Fact]
    public async Task Unsubscribe_post_rejects_invalid_token()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedAsync(BigExpenseScenario().Scenario);

        var anon = factory.CreateClient();
        var res = await anon.PostAsync("/nudges/unsubscribe?token=not-a-real-token", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
