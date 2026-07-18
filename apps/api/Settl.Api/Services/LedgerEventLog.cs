using System.Globalization;
using System.Text.Json;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Features;

namespace Settl.Api.Services;

/// <summary>
/// Records <see cref="LedgerEvent"/>s for the v1 trust triggers (trust-notifications-v1
/// spec). Each method builds the denormalized snapshot + affected-member set and ADDS the
/// event to the context — the caller's existing <c>SaveChangesAsync</c> commits it in the same
/// transaction as the change, so a mutation and its audit row are atomic.
/// </summary>
public static class LedgerEventLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>EntryDeleted — snapshots the entry (title + amount) before it is removed.</summary>
    public static void RecordEntryDeleted(SettlDbContext db, HouseholdData data, Guid actorId, Entry entry)
    {
        var payload = new LedgerEventPayload
        {
            Title = entry.Title,
            ActorName = Name(data, actorId),
            AmountMinor = entry.AmountMinor
        };
        Add(db, entry.HouseholdId, actorId, LedgerEventType.EntryDeleted,
            AffectedByEntry(entry, actorId), payload, entryId: entry.Id);
    }

    /// <summary>EntryEdited — records only the fields that actually changed (amount, payer,
    /// split). Emits nothing when the diff is empty, so a no-op PUT is silent. Split change is
    /// compared on INTENT (mode + formula), not the frozen minor shares — otherwise a plain
    /// amount edit, which rescales an equal split's shares, would spuriously log a split change.</summary>
    public static void RecordEntryEdited(
        SettlDbContext db, HouseholdData data, Guid actorId, Entry entry,
        long oldAmountMinor, Guid? oldPayerId, SplitMode oldSplitMode,
        IReadOnlyDictionary<Guid, long> oldShareMinor,
        IReadOnlyDictionary<Guid, decimal?> oldFormula)
    {
        var changes = new List<LedgerFieldChange>();

        if (oldAmountMinor != entry.AmountMinor)
            changes.Add(new LedgerFieldChange("amount",
                oldAmountMinor.ToString(CultureInfo.InvariantCulture),
                entry.AmountMinor.ToString(CultureInfo.InvariantCulture)));

        if (oldPayerId != entry.PaidByMemberId)
            changes.Add(new LedgerFieldChange("payer",
                oldPayerId is { } o ? Name(data, o) : null,
                entry.PaidByMemberId is { } n ? Name(data, n) : null));

        var newFormula = entry.Shares.ToDictionary(s => s.MemberId, s => s.FormulaValue);
        if (oldSplitMode != entry.SplitMode || !SameFormula(oldFormula, newFormula))
            changes.Add(new LedgerFieldChange("split",
                Dtos.Contract.SplitMode(oldSplitMode), Dtos.Contract.SplitMode(entry.SplitMode)));

        if (changes.Count == 0) return;

        var payload = new LedgerEventPayload
        {
            Title = entry.Title,
            ActorName = Name(data, actorId),
            AmountMinor = entry.AmountMinor,
            Changes = changes
        };
        // Union of old and new share-holders + payers so a member dropped from the split still
        // hears that they were removed.
        var affected = AffectedByEntry(entry, actorId);
        foreach (var id in oldShareMinor.Where(kv => kv.Value > 0).Select(kv => kv.Key))
            if (id != actorId) affected.Add(id);
        if (oldPayerId is { } op && op != actorId) affected.Add(op);
        Add(db, entry.HouseholdId, actorId, LedgerEventType.EntryEdited, affected, payload, entryId: entry.Id);
    }

    /// <summary>SettlementRecorded — a debt on this entry was marked paid.</summary>
    public static void RecordSettlementRecorded(SettlDbContext db, HouseholdData data, Guid actorId, Entry entry)
    {
        var payload = new LedgerEventPayload
        {
            Title = entry.Title,
            ActorName = Name(data, actorId),
            AmountMinor = entry.AmountMinor
        };
        Add(db, entry.HouseholdId, actorId, LedgerEventType.SettlementRecorded,
            AffectedByEntry(entry, actorId), payload, entryId: entry.Id);
    }

    /// <summary>RecurringChanged — amount/cadence/next-date of a recurring cost changed. Whole
    /// household (minus actor) is potentially charged, so all are notified.</summary>
    public static void RecordRecurringChanged(
        SettlDbContext db, HouseholdData data, Guid actorId, RecurringTemplate template,
        List<LedgerFieldChange> changes)
    {
        if (changes.Count == 0) return;

        var payload = new LedgerEventPayload
        {
            Title = template.Title,
            ActorName = Name(data, actorId),
            AmountMinor = template.AmountMinor,
            Changes = changes
        };
        var affected = data.OrderedMemberIds.Where(id => id != actorId).ToHashSet();
        Add(db, template.HouseholdId, actorId, LedgerEventType.RecurringChanged,
            affected, payload, recurringTemplateId: template.Id);
    }

    private static void Add(
        SettlDbContext db, Guid householdId, Guid actorId, LedgerEventType type,
        HashSet<Guid> affected, LedgerEventPayload payload,
        Guid? entryId = null, Guid? recurringTemplateId = null)
    {
        // No one to tell → no event. Keeps the log free of self-only changes.
        if (affected.Count == 0) return;

        db.LedgerEvents.Add(new LedgerEvent
        {
            Id = Guid.NewGuid(),
            HouseholdId = householdId,
            ActorMemberId = actorId,
            Type = type,
            EntryId = entryId,
            RecurringTemplateId = recurringTemplateId,
            AffectedMemberIdsCsv = string.Join(',', affected),
            PayloadJson = JsonSerializer.Serialize(payload, Json),
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Members financially party to an entry (payer + positive share-holders), minus
    /// the actor — you are never notified of your own change.</summary>
    private static HashSet<Guid> AffectedByEntry(Entry entry, Guid actorId)
    {
        var set = entry.Shares.Where(s => s.ShareMinor > 0).Select(s => s.MemberId).ToHashSet();
        if (entry.PaidByMemberId is { } payer) set.Add(payer);
        set.Remove(actorId);
        return set;
    }

    private static bool SameFormula(
        IReadOnlyDictionary<Guid, decimal?> a, IReadOnlyDictionary<Guid, decimal?> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (k, v) in a)
            if (!b.TryGetValue(k, out var bv) || bv != v) return false;
        return true;
    }

    private static string Name(HouseholdData data, Guid memberId) =>
        data.MembersById.TryGetValue(memberId, out var m) ? m.Name : "Någon";
}
