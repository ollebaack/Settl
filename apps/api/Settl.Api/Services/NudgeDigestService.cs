using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Dtos;
using Settl.Api.Features;

namespace Settl.Api.Services;

/// <summary>
/// The daily nudge-digest pass (reminder-delivery spec). For each opted-in member it
/// recomputes their current nudges across every household they belong to (reusing
/// <see cref="NudgeComputation"/> so the digest and the in-app feed never diverge), diffs them
/// against the emitted-nudge log, and — if anything is un-sent — emails ONE digest and records the
/// sent keys so a standing condition is never re-mailed. Silent when there's nothing new.
///
/// Not a hosted service of its own: the reminder-delivery spec mandates extending the existing
/// worker, so <see cref="RecurringPostingService"/> owns the schedule and calls this per pass.
/// </summary>
public sealed class NudgeDigestService(
    SettlDbContext db,
    IEmailSender email,
    NudgeUnsubscribeTokens tokens,
    IConfiguration config,
    ILogger<NudgeDigestService> logger)
{
    /// <summary>Runs one digest pass. <paramref name="now"/> stamps the sent rows (UTC);
    /// <paramref name="today"/> drives the nudge windows. Returns the number of digests sent.</summary>
    public async Task<int> RunAsync(DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var baseUrl = config["Web:BaseUrl"] ?? "http://localhost:5173";
        var localDayStartUtc = DigestSchedule.LocalDayStartUtc(now);

        // Only members who opted in AND can actually receive mail (confirmed address).
        var candidates = await db.Members
            .Where(m => m.NudgeEmailsEnabled && m.EmailConfirmed && m.Email != null)
            .Select(m => new { m.Id, m.Name, Email = m.Email!, m.NudgeTone })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var member in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // This member's whole emitted-nudge log — small, and it backs both guards below.
                // SentAt is compared in memory because SQLite (tests) can't translate DateTimeOffset
                // comparisons (same reason as ExpiredInviteScrubber).
                var priorLog = await db.EmittedNudges
                    .Where(en => en.MemberId == member.Id)
                    .Select(en => new { en.NudgeKey, en.SentAt })
                    .ToListAsync(ct);

                // Once-a-day guard that survives a restart: skip anyone already mailed today (local).
                if (priorLog.Any(l => l.SentAt >= localDayStartUtc)) continue;

                var tone = Contract.NudgeTone(member.NudgeTone);
                var householdIds = await db.Households
                    .Where(h => h.ArchivedAt == null && h.Memberships.Any(hm => hm.MemberId == member.Id))
                    .Select(h => h.Id)
                    .ToListAsync(ct);

                var emittable = new List<NudgeCalculator.EmittableNudge>();
                foreach (var hid in householdIds)
                {
                    var nudges = await NudgeComputation.ForMember(db, hid, member.Id, tone, today, ct);
                    if (nudges is not null) emittable.AddRange(nudges);
                }
                if (emittable.Count == 0) continue;

                // Diff against what we've already emailed this member — the load-bearing dedup.
                var alreadySentSet = priorLog.Select(l => l.NudgeKey).ToHashSet();
                var unsent = emittable.Where(e => !alreadySentSet.Contains(e.Key)).ToList();
                if (unsent.Count == 0) continue;

                var lines = unsent
                    .Select(e => new NudgeDigestLine(e.Nudge.Title, e.Nudge.Body, e.Nudge.When))
                    .ToList();
                var unsubscribeUrl = $"{baseUrl}/nudges/unsubscribe?token={Uri.EscapeDataString(tokens.Create(member.Id))}";

                // Send first, record second: a send failure leaves no log rows, so it retries next
                // pass rather than silently swallowing the nudge. The unique (member, key) index keeps
                // a duplicate insert harmless if a record ever races.
                await email.SendNudgeDigestEmailAsync(member.Email, member.Name, lines, unsubscribeUrl, ct);

                foreach (var e in unsent)
                    db.EmittedNudges.Add(new EmittedNudge
                    {
                        Id = Guid.NewGuid(),
                        MemberId = member.Id,
                        NudgeKey = e.Key,
                        SentAt = now
                    });
                await db.SaveChangesAsync(ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Isolate each member: a bad address or transient send error must not abort the pass.
                logger.LogError(ex, "Nudge digest failed for member {MemberId}", member.Id);
            }
        }

        if (sent > 0) logger.LogInformation("Sent {Count} nudge digest(s)", sent);
        return sent;
    }
}
