using Settl.Api.Domain;

namespace Settl.Api.Tests;

/// <summary>
/// Unit tests for the pure <see cref="DigestSchedule"/> — the "when does the daily digest go out"
/// policy. The point of the local-zone handling (reminder-delivery spec) is that an "08:00" send
/// is 08:00 in Sweden, not 08:00 UTC (which is 09:00/10:00 local) and never 02:00 local. These
/// pin that across both DST offsets.
/// </summary>
public sealed class DigestScheduleTests
{
    private static DateTimeOffset Utc(int y, int mo, int d, int h, int mi) =>
        new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void IsPastSendHour_Summer_TrueFromLocalEight_NotUtcEight()
    {
        // July: Sweden is UTC+2 (CEST). Local 08:00 == 06:00 UTC.
        Assert.False(DigestSchedule.IsPastSendHour(Utc(2026, 7, 15, 5, 59))); // 07:59 local
        Assert.True(DigestSchedule.IsPastSendHour(Utc(2026, 7, 15, 6, 0)));   // 08:00 local
        // 06:00 UTC is already past the send hour — proving we don't (wrongly) wait for 08:00 UTC.
        Assert.True(DigestSchedule.IsPastSendHour(Utc(2026, 7, 15, 6, 30)));
    }

    [Fact]
    public void IsPastSendHour_Winter_TrueFromLocalEight()
    {
        // January: Sweden is UTC+1 (CET). Local 08:00 == 07:00 UTC.
        Assert.False(DigestSchedule.IsPastSendHour(Utc(2026, 1, 15, 6, 59))); // 07:59 local
        Assert.True(DigestSchedule.IsPastSendHour(Utc(2026, 1, 15, 7, 0)));   // 08:00 local
    }

    [Fact]
    public void IsPastSendHour_EarlyUtc_IsStillNightLocal_NotSent()
    {
        // 02:00 UTC in summer is 04:00 local — comfortably before the send hour, so no 02:00-local
        // (or pre-dawn) sends. This is the exact footgun the spec calls out.
        Assert.False(DigestSchedule.IsPastSendHour(Utc(2026, 7, 15, 2, 0)));
    }

    [Fact]
    public void LocalDate_RollsOverAtLocalMidnight_NotUtcMidnight()
    {
        // 23:30 UTC in summer is 01:30 the NEXT day local.
        Assert.Equal(new DateOnly(2026, 7, 16), DigestSchedule.LocalDate(Utc(2026, 7, 15, 23, 30)));
    }

    [Fact]
    public void ShouldRun_TrueOncePerLocalDay()
    {
        var afterSend = Utc(2026, 7, 15, 7, 0); // 09:00 local
        // Not yet run today → run.
        Assert.True(DigestSchedule.ShouldRun(afterSend, lastRunLocalDate: null));
        // Already ran today → skip.
        Assert.False(DigestSchedule.ShouldRun(afterSend, lastRunLocalDate: new DateOnly(2026, 7, 15)));
        // Ran yesterday → run again today.
        Assert.True(DigestSchedule.ShouldRun(afterSend, lastRunLocalDate: new DateOnly(2026, 7, 14)));
    }

    [Fact]
    public void ShouldRun_FalseBeforeSendHour_EvenIfNotRunYet()
    {
        var beforeSend = Utc(2026, 7, 15, 3, 0); // 05:00 local
        Assert.False(DigestSchedule.ShouldRun(beforeSend, lastRunLocalDate: null));
    }

    [Fact]
    public void LocalDayStartUtc_IsLocalMidnightExpressedInUtc()
    {
        // Summer local midnight of 2026-07-15 is 2025-07-14 22:00 UTC (UTC+2).
        Assert.Equal(Utc(2026, 7, 14, 22, 0), DigestSchedule.LocalDayStartUtc(Utc(2026, 7, 15, 6, 0)));
    }
}
