using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Services;

/// <summary>
/// Posts due recurring cycles: catch-up on startup, then on a timer. Idempotent — a post for
/// (templateId, postDate) is created once (guarded by a check plus the unique DB index). Pausing
/// a template (Active=false) stops posting without deleting history; resuming continues from
/// NextPostDate. Posting delegates to pure <see cref="RecurrenceCalculator"/>/<see cref="RecurringPoster"/>.
/// </summary>
public sealed class RecurringPostingService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringPostingService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

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
        try
        {
            await PostDueCycles(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recurring posting run failed");
        }
    }

    /// <summary>Posts all missed cycles up to today for every active template, in one transaction.</summary>
    public async Task PostDueCycles(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var templates = await db.RecurringTemplates
            .Where(t => t.Active && t.NextPostDate <= today)
            .Include(t => t.Shares)
            .Include(t => t.Household).ThenInclude(h => h.Memberships)
            .ToListAsync(ct);

        if (templates.Count == 0) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var posted = 0;

        foreach (var template in templates)
        {
            var order = MembershipOrder.Order(template.Household.Memberships);

            foreach (var postDate in RecurrenceCalculator.DuePosts(
                         template.Active, template.NextPostDate, template.Cadence, today))
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

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        if (posted > 0)
            logger.LogInformation("Posted {Count} recurring cycle(s)", posted);
    }
}
