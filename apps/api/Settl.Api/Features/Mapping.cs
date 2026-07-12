using Settl.Api.Domain;
using Settl.Api.Dtos;

namespace Settl.Api.Features;

/// <summary>Maps loaded entities into contract DTOs. Derivation goes through the pure calculators.</summary>
public static class Mapping
{
    public static EntryDto ToEntryDto(
        Entry entry,
        IReadOnlyList<Guid> orderedMemberIds,
        IReadOnlyDictionary<Guid, Member> membersById,
        IReadOnlyDictionary<Guid, string> templateTitles,
        ClosureLookup closures,
        Guid me)
    {
        var settled = BalanceCalculator.IsSettled(entry, closures);
        var locked = BalanceCalculator.IsLocked(entry, closures);
        var status = BalanceCalculator.StatusFor(entry, me, closures);

        IReadOnlyList<ShareDto> shares = entry.Type == EntryType.Iou
            ? []
            : entry.Shares
                .OrderBy(s => IndexOf(orderedMemberIds, s.MemberId))
                .Select(s => new ShareDto(
                    s.MemberId,
                    Name(membersById, s.MemberId),
                    Color(membersById, s.MemberId),
                    s.ShareMinor,
                    s.MemberId == entry.PaidByMemberId))
                .ToList();

        string? templateTitle = entry.RecurringTemplateId is { } tid && templateTitles.TryGetValue(tid, out var t)
            ? t
            : null;

        return new EntryDto(
            entry.Id,
            entry.HouseholdId,
            Contract.EntryType(entry.Type),
            entry.Title,
            entry.AmountMinor,
            entry.Date,
            entry.CreatedAt,
            entry.PaidByMemberId,
            entry.FromMemberId,
            entry.ToMemberId,
            Contract.SplitMode(entry.SplitMode),
            shares,
            entry.RecurringTemplateId,
            templateTitle,
            settled,
            locked,
            new ViewerStatusDto(Contract.ViewerStatusKind(status.Kind), status.AmountMinor));
    }

    /// <summary>Frozen shares for a template's current formula, in membership order.</summary>
    public static IReadOnlyList<ShareFreezer.FrozenShare> TemplateShares(
        RecurringTemplate template, IReadOnlyList<Guid> orderedMemberIds)
    {
        var formula = template.Shares.ToDictionary(s => s.MemberId, s => s.FormulaValue ?? 0m);
        return ShareFreezer.Freeze(template.SplitMode, orderedMemberIds, template.AmountMinor, formula);
    }

    public static RecurringDto ToRecurringDto(
        RecurringTemplate template,
        IReadOnlyList<Guid> orderedMemberIds,
        IReadOnlyDictionary<Guid, Member> membersById,
        Guid me,
        DateOnly today)
    {
        var shares = TemplateShares(template, orderedMemberIds);
        var yourShare = shares.Where(s => s.MemberId == me).Sum(s => s.ShareMinor);
        var contributing = shares.Where(s => s.ShareMinor > 0).Select(s => s.MemberId).ToList();

        return new RecurringDto(
            template.Id,
            template.Title,
            template.AmountMinor,
            Contract.Cadence(template.Cadence),
            template.NextPostDate,
            SwedishDates.DaysUntil(template.NextPostDate, today),
            template.Active,
            Name(membersById, template.PaidByMemberId),
            Contract.SplitMode(template.SplitMode),
            yourShare,
            RecurrenceCalculator.MonthlyNormalizedMinor(template.AmountMinor, template.Cadence),
            RecurrenceCalculator.CycleProgress(template.NextPostDate, template.Cadence, today, template.Active),
            contributing);
    }

    private static int IndexOf(IReadOnlyList<Guid> order, Guid id)
    {
        for (var i = 0; i < order.Count; i++)
            if (order[i] == id) return i;
        return int.MaxValue;
    }

    public static string Name(IReadOnlyDictionary<Guid, Member> members, Guid id) =>
        members.TryGetValue(id, out var m) ? m.Name : "?";

    private static string Color(IReadOnlyDictionary<Guid, Member> members, Guid id) =>
        members.TryGetValue(id, out var m) ? m.AvatarColor : "#cccccc";
}
