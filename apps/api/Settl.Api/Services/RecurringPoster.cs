using Settl.Api.Domain;

namespace Settl.Api.Services;

/// <summary>
/// Builds a posted <see cref="Entry"/> for one recurring cycle. Delegates share math to the
/// pure <see cref="ShareFreezer"/>/<see cref="SplitCalculator"/> so posting is unit-testable.
/// Re-splits from the template's CURRENT formula each cycle (editing a split affects only future cycles).
/// </summary>
public static class RecurringPoster
{
    public static Entry BuildPost(
        RecurringTemplate template,
        IReadOnlyList<Guid> membersInOrder,
        DateOnly postDate,
        DateTimeOffset now)
    {
        var formula = template.Shares.ToDictionary(s => s.MemberId, s => s.FormulaValue ?? 0m);
        var frozen = ShareFreezer.Freeze(template.SplitMode, membersInOrder, template.AmountMinor, formula);

        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            HouseholdId = template.HouseholdId,
            Type = EntryType.RecurringPost,
            Title = $"{template.Title} — {SwedishDates.FullMonth(postDate)}",
            AmountMinor = template.AmountMinor,
            Date = postDate,
            CreatedAt = now,
            PaidByMemberId = template.PaidByMemberId,
            SplitMode = template.SplitMode,
            RecurringTemplateId = template.Id
        };

        foreach (var s in frozen)
            entry.Shares.Add(new EntryShare
            {
                EntryId = entry.Id,
                MemberId = s.MemberId,
                ShareMinor = s.ShareMinor,
                FormulaValue = s.FormulaValue
            });

        return entry;
    }
}
