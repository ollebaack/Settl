using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Settl.Api.Domain;
using Settl.Api.Services;
using Settl.Api.Tests.Infrastructure;

namespace Settl.Api.Tests;

/// <summary>
/// Recurrence slice: the pure <see cref="RecurrenceCalculator"/> and <see cref="RecurringPoster"/>
/// functions, plus the catch-up / idempotency behaviour of
/// <see cref="RecurringPostingService.PostDueCycles"/>. Values asserted here are computed from the
/// contract (docs/design/api-contract.md §4) and the source under test, not guessed.
/// </summary>
public class RecurrenceTests
{
    // ---------------------------------------------------------------- Advance

    [Fact]
    public void Advance_Monthly_adds_one_calendar_month()
    {
        var result = RecurrenceCalculator.Advance(new DateOnly(2026, 7, 15), Cadence.Monthly);
        Assert.Equal(new DateOnly(2026, 8, 15), result);
    }

    [Fact]
    public void Advance_Monthly_clamps_month_end_Jan31_to_Feb()
    {
        // DateOnly.AddMonths clamps to the last valid day: 2025 is not a leap year, so Feb 28.
        var result = RecurrenceCalculator.Advance(new DateOnly(2025, 1, 31), Cadence.Monthly);
        Assert.Equal(new DateOnly(2025, 2, 28), result);
    }

    [Fact]
    public void Advance_Monthly_clamps_Jan31_to_Feb29_in_leap_year()
    {
        var result = RecurrenceCalculator.Advance(new DateOnly(2024, 1, 31), Cadence.Monthly);
        Assert.Equal(new DateOnly(2024, 2, 29), result);
    }

    [Fact]
    public void Advance_Biweekly_adds_fourteen_days()
    {
        var result = RecurrenceCalculator.Advance(new DateOnly(2026, 1, 1), Cadence.Biweekly);
        Assert.Equal(new DateOnly(2026, 1, 15), result);
    }

    [Fact]
    public void Advance_Weekly_adds_seven_days()
    {
        var result = RecurrenceCalculator.Advance(new DateOnly(2026, 1, 1), Cadence.Weekly);
        Assert.Equal(new DateOnly(2026, 1, 8), result);
    }

    // ------------------------------------------------------------ CycleProgress

    [Fact]
    public void CycleProgress_is_zero_when_inactive()
    {
        var today = new DateOnly(2026, 7, 12);
        // Even a mid-cycle date returns 0 when the template is paused.
        var progress = RecurrenceCalculator.CycleProgress(
            today.AddDays(3), Cadence.Monthly, today, active: false);
        Assert.Equal(0d, progress);
    }

    [Fact]
    public void CycleProgress_due_in_three_days_monthly_is_about_point_nine()
    {
        var today = new DateOnly(2026, 7, 12);
        // daysLeft = 3, cycle length 30 → 1 - 3/30 = 0.9.
        var progress = RecurrenceCalculator.CycleProgress(
            today.AddDays(3), Cadence.Monthly, today, active: true);
        Assert.Equal(0.9d, progress, 3);
    }

    [Fact]
    public void CycleProgress_clamps_to_lower_bound_at_cycle_start()
    {
        var today = new DateOnly(2026, 7, 12);
        // daysLeft = 30, cycle length 30 → raw 0 → clamped to 0.04.
        var progress = RecurrenceCalculator.CycleProgress(
            today.AddDays(30), Cadence.Monthly, today, active: true);
        Assert.Equal(0.04d, progress, 3);
    }

    [Fact]
    public void CycleProgress_clamps_to_lower_bound_when_far_in_future()
    {
        var today = new DateOnly(2026, 7, 12);
        // daysLeft = 100 → raw strongly negative → clamped to floor 0.04.
        var progress = RecurrenceCalculator.CycleProgress(
            today.AddDays(100), Cadence.Monthly, today, active: true);
        Assert.Equal(0.04d, progress, 3);
    }

    [Fact]
    public void CycleProgress_clamps_to_one_when_overdue()
    {
        var today = new DateOnly(2026, 7, 12);
        // daysLeft = -5 → raw = 1 + 5/30 ≈ 1.167 → clamped to ceiling 1.0.
        var progress = RecurrenceCalculator.CycleProgress(
            today.AddDays(-5), Cadence.Monthly, today, active: true);
        Assert.Equal(1.0d, progress, 3);
    }

    // --------------------------------------------------- MonthlyNormalizedMinor

    [Fact]
    public void MonthlyNormalizedMinor_monthly_is_unchanged()
    {
        Assert.Equal(1000L, RecurrenceCalculator.MonthlyNormalizedMinor(1000, Cadence.Monthly));
    }

    [Fact]
    public void MonthlyNormalizedMinor_biweekly_doubles()
    {
        Assert.Equal(2000L, RecurrenceCalculator.MonthlyNormalizedMinor(1000, Cadence.Biweekly));
    }

    [Fact]
    public void MonthlyNormalizedMinor_weekly_quadruples()
    {
        Assert.Equal(4000L, RecurrenceCalculator.MonthlyNormalizedMinor(1000, Cadence.Weekly));
    }

    // --------------------------------------------------------------- DuePosts

    [Fact]
    public void DuePosts_is_empty_when_next_post_is_in_the_future()
    {
        var today = new DateOnly(2026, 7, 12);
        var due = RecurrenceCalculator.DuePosts(
            active: true, nextPostDate: today.AddDays(5), Cadence.Monthly, today).ToList();
        Assert.Empty(due);
    }

    [Fact]
    public void DuePosts_yields_every_missed_cycle_up_to_and_including_today()
    {
        var today = new DateOnly(2026, 7, 12);
        // Weekly, starting two cycles ago: expect today-14, today-7, today (inclusive).
        var due = RecurrenceCalculator.DuePosts(
            active: true, nextPostDate: today.AddDays(-14), Cadence.Weekly, today).ToList();

        Assert.Equal(
            new[] { today.AddDays(-14), today.AddDays(-7), today },
            due);
    }

    [Fact]
    public void DuePosts_is_empty_when_inactive_even_if_overdue()
    {
        var today = new DateOnly(2026, 7, 12);
        var due = RecurrenceCalculator.DuePosts(
            active: false, nextPostDate: today.AddDays(-30), Cadence.Monthly, today).ToList();
        Assert.Empty(due);
    }

    [Fact]
    public void DuePosts_terminates_over_a_large_backlog()
    {
        // Weekly from Jan 1 to Mar 1 2025 (59 days). Offsets 0,7,..,56 fit; 63 does not → 9 posts.
        var start = new DateOnly(2025, 1, 1);
        var today = new DateOnly(2025, 3, 1);
        var due = RecurrenceCalculator.DuePosts(
            active: true, nextPostDate: start, Cadence.Weekly, today).ToList();

        Assert.Equal(9, due.Count);
        Assert.Equal(start, due[0]);
        Assert.Equal(new DateOnly(2025, 2, 26), due[^1]); // Jan 1 + 56 days
        Assert.True(due[^1].DayNumber <= today.DayNumber);
    }

    // --------------------------------------------------- RecurringPoster.BuildPost

    [Fact]
    public void BuildPost_sets_title_type_amount_payer_date_and_template_link()
    {
        var payer = Guid.NewGuid();
        var other = Guid.NewGuid();
        var order = new[] { payer, other };
        var template = new RecurringTemplate
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Hyra",
            AmountMinor = 900000,
            Cadence = Cadence.Monthly,
            SplitMode = SplitMode.Equal,
            PaidByMemberId = payer
        };
        foreach (var m in order)
            template.Shares.Add(new RecurringShare
            {
                RecurringTemplateId = template.Id,
                MemberId = m,
                FormulaValue = null
            });

        var postDate = new DateOnly(2026, 7, 1);
        var now = new DateTimeOffset(2026, 7, 1, 3, 0, 0, TimeSpan.Zero);

        var entry = RecurringPoster.BuildPost(template, order, postDate, now);

        Assert.Equal("Hyra — juli", entry.Title); // full Swedish month of the post date
        Assert.Equal(EntryType.RecurringPost, entry.Type);
        Assert.Equal(900000, entry.AmountMinor);
        Assert.Equal(payer, entry.PaidByMemberId);
        Assert.Equal(postDate, entry.Date);
        Assert.Equal(now, entry.CreatedAt);
        Assert.Equal(template.Id, entry.RecurringTemplateId);
        Assert.Equal(template.HouseholdId, entry.HouseholdId);
        Assert.Equal(SplitMode.Equal, entry.SplitMode);
    }

    [Fact]
    public void BuildPost_freezes_equal_shares_with_deterministic_remainder()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();
        var order = new[] { m1, m2, m3 };
        var template = new RecurringTemplate
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Internet",
            AmountMinor = 10001, // 3-way: base 3333, +1 öre to first two members
            Cadence = Cadence.Monthly,
            SplitMode = SplitMode.Equal,
            PaidByMemberId = m1
        };
        foreach (var m in order)
            template.Shares.Add(new RecurringShare { RecurringTemplateId = template.Id, MemberId = m });

        var entry = RecurringPoster.BuildPost(
            template, order, new DateOnly(2026, 3, 1), DateTimeOffset.UtcNow);

        var byMember = entry.Shares.ToDictionary(s => s.MemberId, s => s.ShareMinor);
        Assert.Equal(3334, byMember[m1]);
        Assert.Equal(3334, byMember[m2]);
        Assert.Equal(3333, byMember[m3]);
        Assert.Equal(10001, entry.Shares.Sum(s => s.ShareMinor));
        Assert.All(entry.Shares, s => Assert.Null(s.FormulaValue)); // Equal carries no formula
    }

    [Fact]
    public void BuildPost_freezes_percent_shares_from_the_templates_current_formula()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var m3 = Guid.NewGuid();
        var order = new[] { m1, m2, m3 };
        var template = new RecurringTemplate
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "El",
            AmountMinor = 10000,
            Cadence = Cadence.Monthly,
            SplitMode = SplitMode.Percent,
            PaidByMemberId = m1
        };
        template.Shares.Add(new RecurringShare { RecurringTemplateId = template.Id, MemberId = m1, FormulaValue = 50m });
        template.Shares.Add(new RecurringShare { RecurringTemplateId = template.Id, MemberId = m2, FormulaValue = 30m });
        template.Shares.Add(new RecurringShare { RecurringTemplateId = template.Id, MemberId = m3, FormulaValue = 20m });

        var entry = RecurringPoster.BuildPost(
            template, order, new DateOnly(2026, 5, 1), DateTimeOffset.UtcNow);

        var shareByMember = entry.Shares.ToDictionary(s => s.MemberId, s => s.ShareMinor);
        Assert.Equal(5000, shareByMember[m1]); // 50% of 10000
        Assert.Equal(3000, shareByMember[m2]); // 30%
        Assert.Equal(2000, shareByMember[m3]); // 20%
        Assert.Equal(10000, entry.Shares.Sum(s => s.ShareMinor));

        // The frozen formula value is carried through per member.
        var formulaByMember = entry.Shares.ToDictionary(s => s.MemberId, s => s.FormulaValue);
        Assert.Equal(50m, formulaByMember[m1]);
        Assert.Equal(30m, formulaByMember[m2]);
        Assert.Equal(20m, formulaByMember[m3]);
    }

    [Fact]
    public void BuildPost_reflects_edits_to_the_current_formula_on_the_next_cycle()
    {
        var m1 = Guid.NewGuid();
        var m2 = Guid.NewGuid();
        var order = new[] { m1, m2 };
        var template = new RecurringTemplate
        {
            Id = Guid.NewGuid(),
            HouseholdId = Guid.NewGuid(),
            Title = "Städ",
            AmountMinor = 10000,
            Cadence = Cadence.Monthly,
            SplitMode = SplitMode.Percent,
            PaidByMemberId = m1
        };
        var s1 = new RecurringShare { RecurringTemplateId = template.Id, MemberId = m1, FormulaValue = 50m };
        var s2 = new RecurringShare { RecurringTemplateId = template.Id, MemberId = m2, FormulaValue = 50m };
        template.Shares.Add(s1);
        template.Shares.Add(s2);

        var first = RecurringPoster.BuildPost(template, order, new DateOnly(2026, 1, 1), DateTimeOffset.UtcNow);
        Assert.Equal(5000, first.Shares.Single(s => s.MemberId == m1).ShareMinor);

        // Edit the template's current formula; the next posted cycle re-splits from it.
        s1.FormulaValue = 70m;
        s2.FormulaValue = 30m;
        var second = RecurringPoster.BuildPost(template, order, new DateOnly(2026, 2, 1), DateTimeOffset.UtcNow);
        Assert.Equal(7000, second.Shares.Single(s => s.MemberId == m1).ShareMinor);
        Assert.Equal(3000, second.Shares.Single(s => s.MemberId == m2).ShareMinor);
    }

    // ---------------------------------------- PostDueCycles (integration + idempotency)

    private static RecurringPostingService NewService(SettlApiFactory factory)
    {
        var scopeFactory = factory.Services.GetRequiredService<IServiceScopeFactory>();
        return new RecurringPostingService(scopeFactory, NullLogger<RecurringPostingService>.Instance);
    }

    [Fact]
    public async Task PostDueCycles_posts_missed_cycles_and_advances_next_post_date()
    {
        using var factory = new SettlApiFactory();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var scenario = new TestScenario();
        scenario.AddMember("A");
        scenario.AddMember("B");
        scenario.AddMember("C");
        // Weekly, two cycles behind → due at today-14, today-7, today (3 posts).
        var template = scenario.AddRecurring(
            "Hyra", 900000, scenario.MemberIds[0], Cadence.Weekly, nextPostOffset: -14);
        await factory.SeedAsync(scenario);

        await NewService(factory).PostDueCycles(CancellationToken.None);

        var posts = await factory.WithDb(db => db.Entries
            .Where(e => e.RecurringTemplateId == template.Id)
            .OrderBy(e => e.Date)
            .ToListAsync());

        Assert.Equal(3, posts.Count);
        Assert.All(posts, p => Assert.Equal(EntryType.RecurringPost, p.Type));
        Assert.All(posts, p => Assert.Equal(900000, p.AmountMinor));
        Assert.Equal(
            new[] { today.AddDays(-14), today.AddDays(-7), today },
            posts.Select(p => p.Date).ToArray());

        // NextPostDate advanced one cadence past the last posted date (today) → today+7.
        var next = await factory.WithDb(db =>
            db.RecurringTemplates.Where(t => t.Id == template.Id).Select(t => t.NextPostDate).SingleAsync());
        Assert.Equal(today.AddDays(7), next);

        // Each post has frozen equal shares summing to the amount.
        foreach (var p in posts)
        {
            var loaded = await factory.WithDb(db => db.EntryShares
                .Where(s => s.EntryId == p.Id).ToListAsync());
            Assert.Equal(3, loaded.Count);
            Assert.Equal(900000, loaded.Sum(s => s.ShareMinor));
        }
    }

    [Fact]
    public async Task PostDueCycles_is_idempotent_on_a_second_run()
    {
        using var factory = new SettlApiFactory();

        var scenario = new TestScenario();
        scenario.AddMember("A");
        scenario.AddMember("B");
        var template = scenario.AddRecurring(
            "Internet", 20000, scenario.MemberIds[0], Cadence.Weekly, nextPostOffset: -14);
        await factory.SeedAsync(scenario);

        await NewService(factory).PostDueCycles(CancellationToken.None);
        var afterFirst = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == template.Id));
        Assert.Equal(3, afterFirst);

        // Second run: NextPostDate is now in the future, so nothing is selected/posted.
        await NewService(factory).PostDueCycles(CancellationToken.None);
        var afterSecond = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == template.Id));
        Assert.Equal(3, afterSecond);
    }

    [Fact]
    public async Task PostDueCycles_does_not_double_post_when_next_post_date_is_reset_into_the_past()
    {
        // Directly exercises the (RecurringTemplateId, Date) idempotency guard: re-run the same
        // due dates while their entries already exist. No duplicates are created.
        using var factory = new SettlApiFactory();

        var scenario = new TestScenario();
        scenario.AddMember("A");
        scenario.AddMember("B");
        var template = scenario.AddRecurring(
            "Spotify", 11900, scenario.MemberIds[0], Cadence.Weekly, nextPostOffset: -14);
        var originalNext = template.NextPostDate;
        await factory.SeedAsync(scenario);

        await NewService(factory).PostDueCycles(CancellationToken.None);
        var afterFirst = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == template.Id));
        Assert.Equal(3, afterFirst);

        // Rewind NextPostDate to the original past value so the same dates are "due" again.
        await factory.WithDb(async db =>
        {
            var t = await db.RecurringTemplates.SingleAsync(x => x.Id == template.Id);
            t.NextPostDate = originalNext;
            await db.SaveChangesAsync();
        });

        await NewService(factory).PostDueCycles(CancellationToken.None);
        var afterSecond = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == template.Id));
        Assert.Equal(3, afterSecond); // existing (templateId, date) rows are skipped, not duplicated
    }

    [Fact]
    public async Task PostDueCycles_ignores_paused_templates_and_leaves_their_state_intact()
    {
        using var factory = new SettlApiFactory();

        var scenario = new TestScenario();
        scenario.AddMember("A");
        scenario.AddMember("B");
        var template = scenario.AddRecurring(
            "Netflix", 12900, scenario.MemberIds[0], Cadence.Weekly, nextPostOffset: -14);
        template.Active = false; // paused before seeding
        var pausedNext = template.NextPostDate;
        await factory.SeedAsync(scenario);

        await NewService(factory).PostDueCycles(CancellationToken.None);

        // A paused template posts nothing...
        var count = await factory.WithDb(db =>
            db.Entries.CountAsync(e => e.RecurringTemplateId == template.Id));
        Assert.Equal(0, count);

        // ...and its NextPostDate is untouched (history/schedule preserved for resume).
        var next = await factory.WithDb(db =>
            db.RecurringTemplates.Where(t => t.Id == template.Id).Select(t => t.NextPostDate).SingleAsync());
        Assert.Equal(pausedNext, next);
    }
}
