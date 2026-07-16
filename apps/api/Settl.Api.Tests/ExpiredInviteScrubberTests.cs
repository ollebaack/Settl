using Microsoft.EntityFrameworkCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Services;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>The GDPR scrub (ADR-0019): a typed invitee number is discarded once the invite
/// expires unaccepted, so the persistent data only ever holds consented relationships.</summary>
public class ExpiredInviteScrubberTests
{
    [Fact]
    public async Task ScrubAsync_nulls_phone_on_expired_unaccepted_invites_only()
    {
        using var factory = new SettlApiFactory();
        await factory.SeedCanonicalAsync();

        var now = DateTimeOffset.UtcNow;
        var expiredId = Guid.NewGuid();
        var liveId = Guid.NewGuid();

        await factory.WithDb(async db =>
        {
            db.Invites.AddRange(
                new Invite
                {
                    Id = expiredId, HouseholdId = null, Channel = InviteChannel.Sms,
                    PhoneNumber = "+46701234567", TokenHash = InviteTokens.Hash(InviteTokens.NewRawToken()),
                    InvitedByMemberId = SeedIds.Du, CreatedAt = now.AddDays(-8), ExpiresAt = now.AddDays(-1),
                },
                new Invite
                {
                    Id = liveId, HouseholdId = null, Channel = InviteChannel.Sms,
                    PhoneNumber = "+46705550000", TokenHash = InviteTokens.Hash(InviteTokens.NewRawToken()),
                    InvitedByMemberId = SeedIds.Du, CreatedAt = now, ExpiresAt = now.AddDays(6),
                });
            await db.SaveChangesAsync();
        });

        var scrubbed = await factory.WithDb(db => ExpiredInviteScrubber.ScrubAsync(db, now));
        Assert.Equal(1, scrubbed);

        await factory.WithDb(async db =>
        {
            Assert.Null((await db.Invites.SingleAsync(i => i.Id == expiredId)).PhoneNumber);
            Assert.Equal("+46705550000", (await db.Invites.SingleAsync(i => i.Id == liveId)).PhoneNumber);
        });
    }
}
