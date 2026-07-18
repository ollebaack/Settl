using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;

namespace Settl.Api.Services;

/// <summary>
/// Discards the raw invitee phone number from SMS invites once they expire (contacts-phone-sms spec / GDPR).
/// A typed number is transient third-party data: the persistent contact graph only holds
/// relationships between consenting members, never a number that never accepted. The number is
/// already scrubbed on accept (in <c>AcceptInvite</c>); this catches invites that expire
/// unaccepted. Runs on a slow timer with a startup delay, mirroring
/// <see cref="RecurringPostingService"/>; the actual scrub is the pure static
/// <see cref="ScrubAsync"/> so it can be tested deterministically.
/// </summary>
public sealed class ExpiredInviteScrubber(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredInviteScrubber> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    // A startup delay keeps this from firing during short-lived test hosts.
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();
                var scrubbed = await ScrubAsync(db, DateTimeOffset.UtcNow, stoppingToken);
                if (scrubbed > 0)
                    logger.LogInformation("Scrubbed phone numbers from {Count} expired invite(s)", scrubbed);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Expired-invite scrub failed");
        }
    }

    /// <summary>Nulls the phone number on every unaccepted invite that has expired by
    /// <paramref name="now"/>. Returns how many rows were scrubbed. The expiry comparison is
    /// done in memory because SQLite (tests) can't translate DateTimeOffset comparisons.</summary>
    public static async Task<int> ScrubAsync(SettlDbContext db, DateTimeOffset now, CancellationToken ct = default)
    {
        var candidates = await db.Invites
            .Where(i => i.PhoneNumber != null && i.AcceptedAt == null)
            .ToListAsync(ct);

        var expired = candidates.Where(i => i.ExpiresAt <= now).ToList();
        foreach (var invite in expired)
            invite.PhoneNumber = null;

        if (expired.Count > 0)
            await db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
