using Settl.Api.Domain;
using Settl.Api.Dtos;
using static Settl.Api.Domain.NudgeCalculator;

namespace Settl.Api.Tests;

/// <summary>
/// Exhaustive unit tests for the pure <see cref="NudgeCalculator"/> (§5 of the API contract).
/// No DB/HTTP — inputs and <c>today</c> are passed directly. Money copy is composed via the real
/// <see cref="Money.FormatKr"/> so the sv-SE non-breaking-space formatting is asserted exactly.
/// </summary>
public sealed class NudgeCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 7, 12);

    private static readonly Guid RecId = Guid.Parse("d1000000-0000-0000-0000-000000000001");
    private static readonly Guid EntryId = Guid.Parse("d2000000-0000-0000-0000-000000000001");
    private static readonly Guid PayerId = Guid.Parse("d3000000-0000-0000-0000-000000000001");
    private static readonly Guid MemberId = Guid.Parse("d4000000-0000-0000-0000-000000000001");

    private static readonly IEnumerable<RecurringDueInput> NoRecurrings = Array.Empty<RecurringDueInput>();
    private static readonly IEnumerable<BigExpenseInput> NoExpenses = Array.Empty<BigExpenseInput>();
    private static readonly IEnumerable<BalanceInput> NoBalances = Array.Empty<BalanceInput>();

    private static RecurringDueInput Rec(int nextOffset, long share = 90000, string title = "Hyra") =>
        new(RecId, title, Today.AddDays(nextOffset), share);

    private static BigExpenseInput Big(
        long amount = 240000, int dateOffset = -2, bool payerIsMe = false, bool settled = false,
        long share = 80000, string title = "Möbler", string payerName = "Sam") =>
        new(EntryId, title, amount, Today.AddDays(dateOffset), PayerId, payerName, share, payerIsMe, settled);

    private static BalanceInput Bal(long net, string name = "Sam") => new(MemberId, name, net);

    private static IReadOnlyList<NudgeDto> BuildRec(string tone, RecurringDueInput r) =>
        Build(tone, Today, new[] { r }, NoExpenses, NoBalances);

    private static IReadOnlyList<NudgeDto> BuildBig(string tone, BigExpenseInput e) =>
        Build(tone, Today, NoRecurrings, new[] { e }, NoBalances);

    private static IReadOnlyList<NudgeDto> BuildBal(string tone, BalanceInput b) =>
        Build(tone, Today, NoRecurrings, NoExpenses, new[] { b });

    // ---------------------------------------------------------------- Recurring due

    [Fact]
    public void RecurringDue_Direct_FiresAtZeroDays_ExactCopy()
    {
        var n = Assert.Single(BuildRec("direct", Rec(0)));
        Assert.Equal("recurringDue", n.Kind);
        Assert.Equal("Hyra dras idag", n.Title);
        Assert.Equal($"Din del är {Money.FormatKr(90000)}. Den bokförs automatiskt.", n.Body);
        Assert.Equal("på gång", n.When);
        var a = Assert.Single(n.Actions);
        Assert.Equal("Visa", a.Label);
        Assert.Equal("viewRecurring", a.Kind);
        Assert.Equal(RecId, a.TargetId);
    }

    [Fact]
    public void RecurringDue_Direct_FiresAtFiveDays_WhenCopy()
    {
        var n = Assert.Single(BuildRec("direct", Rec(5)));
        Assert.Equal("Hyra dras om 5 dagar", n.Title);
    }

    [Fact]
    public void RecurringDue_Direct_Tomorrow_WhenCopy()
    {
        var n = Assert.Single(BuildRec("direct", Rec(1)));
        Assert.Equal("Hyra dras imorgon", n.Title);
    }

    [Fact]
    public void RecurringDue_Gentle_ExactCopy()
    {
        var n = Assert.Single(BuildRec("gentle", Rec(3)));
        Assert.Equal("Hyra bokförs om 3 dagar", n.Title);
        Assert.Equal(
            $"Din del ({Money.FormatKr(90000)}) hamnar i loggboken automatiskt — inget att göra.",
            n.Body);
        Assert.Equal("på gång", n.When);
        Assert.Equal("viewRecurring", Assert.Single(n.Actions).Kind);
    }

    [Fact]
    public void RecurringDue_NotFire_SixDaysAhead()
    {
        Assert.Empty(BuildRec("direct", Rec(6)));
    }

    [Fact]
    public void RecurringDue_NotFire_PastDue()
    {
        Assert.Empty(BuildRec("direct", Rec(-1)));
    }

    // ---------------------------------------------------------------- Big expense

    [Fact]
    public void BigExpense_Direct_PayerNotMe_TwoActions_ExactCopy()
    {
        var n = Assert.Single(BuildBig("direct", Big()));
        Assert.Equal("bigExpense", n.Kind);
        Assert.Equal("Stor utgift: Möbler", n.Title);
        Assert.Equal(
            $"Sam la till {Money.FormatKr(240000)}. Din del är {Money.FormatKr(80000)} — gör upp när du kan.",
            n.Body);
        Assert.Equal(SwedishDates.Short(Today.AddDays(-2)), n.When);

        Assert.Equal(2, n.Actions.Count);
        Assert.Equal("Visa", n.Actions[0].Label);
        Assert.Equal("viewEntry", n.Actions[0].Kind);
        Assert.Equal(EntryId, n.Actions[0].TargetId);
        Assert.Equal("Gör upp", n.Actions[1].Label);
        Assert.Equal("settle", n.Actions[1].Kind);
        Assert.Equal(PayerId, n.Actions[1].TargetId);
    }

    [Fact]
    public void BigExpense_Gentle_Body_ExactCopy()
    {
        var n = Assert.Single(BuildBig("gentle", Big()));
        Assert.Equal("Stor utgift: Möbler", n.Title);
        Assert.Equal(
            $"Sam la till {Money.FormatKr(240000)}. Din del är {Money.FormatKr(80000)}. Ingen brådska.",
            n.Body);
    }

    [Fact]
    public void BigExpense_PayerIsMe_OnlyViewAction()
    {
        var n = Assert.Single(BuildBig("direct", Big(payerIsMe: true)));
        var a = Assert.Single(n.Actions);
        Assert.Equal("viewEntry", a.Kind);
        Assert.Equal(EntryId, a.TargetId);
    }

    [Fact]
    public void BigExpense_NotFire_WhenSettled()
    {
        Assert.Empty(BuildBig("direct", Big(settled: true)));
    }

    [Fact]
    public void BigExpense_NotFire_BelowThreshold()
    {
        Assert.Empty(BuildBig("direct", Big(amount: BigExpenseThresholdMinor - 1)));
    }

    [Fact]
    public void BigExpense_Fire_AtExactThreshold()
    {
        var n = Assert.Single(BuildBig("direct", Big(amount: BigExpenseThresholdMinor)));
        Assert.Equal("bigExpense", n.Kind);
    }

    [Fact]
    public void BigExpense_Fire_ExactlySevenDaysAgo()
    {
        var n = Assert.Single(BuildBig("direct", Big(dateOffset: -BigExpenseWindowDays)));
        Assert.Equal(SwedishDates.Short(Today.AddDays(-7)), n.When);
    }

    [Fact]
    public void BigExpense_NotFire_OlderThanSevenDays()
    {
        Assert.Empty(BuildBig("direct", Big(dateOffset: -(BigExpenseWindowDays + 1))));
    }

    [Fact]
    public void BigExpense_Fire_FutureDatedExpense()
    {
        // daysUntil > 0 is never < -7, so future-dated big expenses still fire.
        var n = Assert.Single(BuildBig("direct", Big(dateOffset: 3)));
        Assert.Equal(SwedishDates.Short(Today.AddDays(3)), n.When);
    }

    // ---------------------------------------------------------------- Balance

    [Fact]
    public void Balance_Direct_IOwe_ExactCopy()
    {
        var n = Assert.Single(BuildBal("direct", Bal(-90000)));
        Assert.Equal("balance", n.Kind);
        Assert.Equal($"Du är skyldig Sam {Money.FormatKr(90000)}", n.Title);
        Assert.Equal("Saldot passerade 750 kr — dags att göra upp.", n.Body);
        Assert.Equal("idag", n.When);
        var a = Assert.Single(n.Actions);
        Assert.Equal("Gör upp", a.Label);
        Assert.Equal("settle", a.Kind);
        Assert.Equal(MemberId, a.TargetId);
    }

    [Fact]
    public void Balance_Direct_TheyOweMe_ExactCopy()
    {
        var n = Assert.Single(BuildBal("direct", Bal(80000)));
        Assert.Equal($"Sam är skyldig dig {Money.FormatKr(80000)}", n.Title);
        Assert.Equal("Saldot passerade 750 kr — dags att göra upp.", n.Body);
    }

    [Fact]
    public void Balance_Gentle_Title_ExactCopy()
    {
        var n = Assert.Single(BuildBal("gentle", Bal(-90000)));
        Assert.Equal("Er nota med Sam växer", n.Title);
        Assert.Equal("Ert saldo passerade 750 kr. Kanske ett bra tillfälle att göra upp.", n.Body);
    }

    [Fact]
    public void Balance_NotFire_JustBelowThreshold()
    {
        Assert.Empty(BuildBal("direct", Bal(BalanceThresholdMinor - 1)));
        Assert.Empty(BuildBal("direct", Bal(-(BalanceThresholdMinor - 1))));
    }

    [Fact]
    public void Balance_Fire_AtExactThreshold_BothDirections()
    {
        var owed = Assert.Single(BuildBal("direct", Bal(BalanceThresholdMinor)));
        Assert.Equal($"Sam är skyldig dig {Money.FormatKr(BalanceThresholdMinor)}", owed.Title);

        var owe = Assert.Single(BuildBal("direct", Bal(-BalanceThresholdMinor)));
        Assert.Equal($"Du är skyldig Sam {Money.FormatKr(BalanceThresholdMinor)}", owe.Title);
    }

    [Fact]
    public void Balance_NotFire_WhenSquare()
    {
        Assert.Empty(BuildBal("direct", Bal(0)));
    }

    // ---------------------------------------------------------------- Tone default

    [Fact]
    public void Tone_UnknownAndEmpty_TreatedAsDirect()
    {
        Assert.Equal("Hyra dras idag", Assert.Single(BuildRec("", Rec(0))).Title);
        Assert.Equal("Hyra dras idag", Assert.Single(BuildRec("whatever", Rec(0))).Title);
        // Only the exact literal "gentle" flips to gentle copy.
        Assert.Equal("Hyra bokförs idag", Assert.Single(BuildRec("gentle", Rec(0))).Title);
    }

    // ---------------------------------------------------------------- Ordering

    [Fact]
    public void Build_EmitsInFixedOrder_RecurringThenBigExpenseThenBalance()
    {
        var nudges = Build(
            "direct", Today,
            new[] { Rec(2) },
            new[] { Big() },
            new[] { Bal(-90000) });

        Assert.Equal(3, nudges.Count);
        Assert.Equal("recurringDue", nudges[0].Kind);
        Assert.Equal("bigExpense", nudges[1].Kind);
        Assert.Equal("balance", nudges[2].Kind);
    }

    [Fact]
    public void Build_PreservesInputOrderWithinEachKind()
    {
        var r1 = new RecurringDueInput(Guid.NewGuid(), "A", Today.AddDays(1), 1000);
        var r2 = new RecurringDueInput(Guid.NewGuid(), "B", Today.AddDays(2), 2000);
        var b1 = Bal(80000, "Sam");
        var b2 = new BalanceInput(Guid.NewGuid(), "Priya", -95000);

        var nudges = Build("direct", Today, new[] { r1, r2 }, NoExpenses, new[] { b1, b2 });

        Assert.Equal(4, nudges.Count);
        Assert.Equal("A dras imorgon", nudges[0].Title);
        Assert.Equal("B dras om 2 dagar", nudges[1].Title);
        Assert.Equal($"Sam är skyldig dig {Money.FormatKr(80000)}", nudges[2].Title);
        Assert.Equal($"Du är skyldig Priya {Money.FormatKr(95000)}", nudges[3].Title);
    }

    [Fact]
    public void Build_NoInputs_ReturnsEmpty()
    {
        Assert.Empty(Build("direct", Today, NoRecurrings, NoExpenses, NoBalances));
    }
}
