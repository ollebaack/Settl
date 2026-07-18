using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Services;

/// <summary>
/// Posts due recurring cycles: catch-up on startup, then on a timer. Idempotent — a post for
/// (templateId, postDate) is created once (guarded by a check plus the unique DB index). Pausing
/// a template (Active=false) stops posting without deleting history; resuming continues from
/// NextPostDate. Posting delegates to pure <see cref="RecurrenceCalculator"/>/<see cref="RecurringPoster"/>.
///
/// Also owns the daily nudge-digest pass (reminder-delivery spec): the spec mandates
/// extending this existing worker rather than adding a second one. The hourly tick checks
/// <see cref="DigestSchedule"/>; once per local day past the send hour it runs
/// <see cref="NudgeDigestService"/>. The two jobs run independently so a failure in one never
/// skips the other.
/// </summary>
public sealed class RecurringPostingService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringPostingService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>Local date of the last digest pass — the in-memory "once per day" latch. Resets on
    /// restart, but the per-member "already mailed today" guard in <see cref="NudgeDigestService"/>
    /// keeps a restart from double-sending.</summary>
    private DateOnly? _lastDigestLocalDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Catch-up on startup, then loop.
        await RunOnceSafely(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceSafely(stoppingToken);
    }

    private async Task RunOnceSafely(CancellationToken ct)
    {
        await SafelyRun("Recurring posting", PostDueCycles, ct);
        await SafelyRun("Nudge digest", RunDigestIfDue, ct);
    }

    private async Task SafelyRun(string label, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        try
        {
            await work(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Label} run failed", label);
        }
    }

    /// <summary>Runs the digest pass at most once per local day, once past the send hour.</summary>
    private async Task RunDigestIfDue(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (!DigestSchedule.ShouldRun(now, _lastDigestLocalDate)) return;

        using var scope = scopeFactory.CreateScope();
        var digest = scope.ServiceProvider.GetRequiredService<NudgeDigestService>();
        // Nudge windows use the UTC date, matching the read-path GET /nudges (the local zone only
        // governs WHEN the digest goes out, not the day-count math).
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        await digest.RunAsync(now, today, ct);
        _lastDigestLocalDate = DigestSchedule.LocalDate(now);
    }

    /// <summary>Posts all missed cycles up to today for every active template, in one transaction.</summary>
    public async Task PostDueCycles(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        // Ended templates (cursor past an inclusive EndDate) are excluded here so they aren't
        // re-selected every tick; DuePosts also gates on EndDate as a belt-and-suspenders.
        var templates = await db.RecurringTemplates
            .Where(t => t.Active && t.NextPostDate <= today && (t.EndDate == null || t.NextPostDate <= t.EndDate))
            .Include(t => t.Shares)
            .Include(t => t.Household).ThenInclude(h => h.Memberships)
            .ToListAsync(ct);

        if (templates.Count == 0) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var posted = 0;

        foreach (var template in templates)
        {
            // Isolate each template: a malformed one (e.g. an inconsistent split) must not
            // abort posting for the others or wedge every future run.
            try
            {
                var order = MembershipOrder.Order(template.Household.Memberships);

                foreach (var postDate in RecurrenceCalculator.DuePosts(
                             template.Active, template.NextPostDate, template.Cadence, today, template.EndDate))
                {
                    var exists = await db.Entries.AnyAsync(
                        e => e.RecurringTemplateId == template.Id && e.Date == postDate, ct);
                    if (!exists)
                    {
                        db.Entries.Add(RecurringPoster.BuildPost(template, order, postDate, now));
                        posted++;
                    }

                    template.NextPostDate = RecurrenceCalculator.Advance(postDate, template.Cadence);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Skipping recurring template {TemplateId} — posting failed", template.Id);
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (posted > 0)
            logger.LogInformation("Posted {Count} recurring cycle(s)", posted);
    }
}
