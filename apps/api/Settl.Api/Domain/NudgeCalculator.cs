using Settl.Api.Dtos;

namespace Settl.Api.Domain;

/// <summary>
/// Pure nudge derivation (§5). No storage, no time source — inputs and `today` are passed in.
/// Emits nudges in fixed order: recurring-due, big-expense, balance. Copy varies only by tone.
/// </summary>
public static class NudgeCalculator
{
    public const int RecurringDueDays = 5;
    public const int BigExpenseWindowDays = 7;
    public const long BigExpenseThresholdMinor = 150_000; // 1500 kr
    public const long BalanceThresholdMinor = 75_000;     // 750 kr

    /// <summary>Days a threshold crossing stays fresh enough to nudge (ADR-0023). Mirrors the
    /// big-expense window so both event nudges age out consistently.</summary>
    public const int BalanceCrossingWindowDays = BigExpenseWindowDays;

    public sealed record RecurringDueInput(Guid RecurringId, string Title, DateOnly NextPostDate, long YourShareMinor);

    public sealed record BigExpenseInput(
        Guid EntryId, string Title, long AmountMinor, DateOnly Date,
        Guid PayerId, string PayerName, long YourShareMinor, bool PayerIsMe, bool Settled);

    /// <summary><paramref name="CrossedOn"/> = date the pair's |net| most recently crossed up
    /// through the threshold (ADR-0023), or null if it never has. Null suppresses the nudge even
    /// when <paramref name="NetMinor"/> is currently over — a standing balance with no fresh
    /// crossing is not news.</summary>
    public sealed record BalanceInput(Guid MemberId, string Name, long NetMinor, DateOnly? CrossedOn);

    public static IReadOnlyList<NudgeDto> Build(
        string tone,
        DateOnly today,
        IEnumerable<RecurringDueInput> activeRecurrings,
        IEnumerable<BigExpenseInput> expenses,
        IEnumerable<BalanceInput> balances)
    {
        var direct = tone != "gentle";
        var nudges = new List<NudgeDto>();

        // 1. Recurring due — active template with 0 ≤ daysUntil(next) ≤ 5.
        foreach (var r in activeRecurrings)
        {
            var days = SwedishDates.DaysUntil(r.NextPostDate, today);
            if (days < 0 || days > RecurringDueDays) continue;

            var when = SwedishDates.InDays(r.NextPostDate, today);
            var share = Money.FormatKr(r.YourShareMinor);
            nudges.Add(new NudgeDto(
                Kind: "recurringDue",
                Title: direct ? $"{r.Title} dras {when}" : $"{r.Title} bokförs {when}",
                Body: direct
                    ? $"Din del är {share}. Den bokförs automatiskt."
                    : $"Din del ({share}) hamnar i loggboken automatiskt — inget att göra.",
                When: "på gång",
                Actions: [new NudgeActionDto("Visa", "viewRecurring", r.RecurringId)]));
        }

        // 2. Big expense — non-Iou, unsettled, ≥1500 kr, within last 7 days.
        foreach (var e in expenses)
        {
            if (e.Settled) continue;
            if (e.AmountMinor < BigExpenseThresholdMinor) continue;
            if (SwedishDates.DaysUntil(e.Date, today) < -BigExpenseWindowDays) continue;

            var amount = Money.FormatKr(e.AmountMinor);
            var share = Money.FormatKr(e.YourShareMinor);
            var actions = new List<NudgeActionDto> { new("Visa", "viewEntry", e.EntryId) };
            if (!e.PayerIsMe) actions.Add(new NudgeActionDto("Gör upp", "settle", e.PayerId));

            nudges.Add(new NudgeDto(
                Kind: "bigExpense",
                Title: $"Stor utgift: {e.Title}",
                Body: direct
                    ? $"{e.PayerName} la till {amount}. Din del är {share} — gör upp när du kan."
                    : $"{e.PayerName} la till {amount}. Din del är {share}. Ingen brådska.",
                When: SwedishDates.Short(e.Date),
                Actions: actions));
        }

        // 3. Balance — fire ONCE when |net with X| crosses 750 kr, not continuously while above
        //    (ADR-0023): currently over the threshold AND the crossing is within the window.
        foreach (var b in balances)
        {
            if (Math.Abs(b.NetMinor) < BalanceThresholdMinor) continue;
            if (b.CrossedOn is not { } crossedOn) continue;
            if (SwedishDates.DaysUntil(crossedOn, today) < -BalanceCrossingWindowDays) continue;

            var amount = Money.FormatKr(b.NetMinor);
            string title;
            if (direct)
                title = b.NetMinor < 0
                    ? $"Du är skyldig {b.Name} {amount}"
                    : $"{b.Name} är skyldig dig {amount}";
            else
                title = $"Er nota med {b.Name} växer";

            nudges.Add(new NudgeDto(
                Kind: "balance",
                Title: title,
                Body: direct
                    ? "Saldot passerade 750 kr — dags att göra upp."
                    : "Ert saldo passerade 750 kr. Kanske ett bra tillfälle att göra upp.",
                When: SwedishDates.Short(crossedOn),
                Actions: [new NudgeActionDto("Gör upp", "settle", b.MemberId)]));
        }

        return nudges;
    }
}
