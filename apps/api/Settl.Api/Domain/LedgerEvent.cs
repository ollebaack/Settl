namespace Settl.Api.Domain;

/// <summary>The kind of state-changing action an <see cref="LedgerEvent"/> records — the v1
/// trust triggers (trust-notifications-v1 spec). Stored as the enum name.</summary>
public enum LedgerEventType
{
    /// <summary>An entry was hard-deleted.</summary>
    EntryDeleted,

    /// <summary>An entry's amount, payer, or split was changed.</summary>
    EntryEdited,

    /// <summary>A debt was marked paid / a settlement was recorded against an entry.</summary>
    SettlementRecorded,

    /// <summary>A recurring template's amount or schedule was changed.</summary>
    RecurringChanged
}

/// <summary>
/// An append-only record of one ledger-changing action, so an affected member can be told
/// "someone changed this" and can never be quietly cheated (trust-notifications-v1 spec). It is immutable audit,
/// not a per-recipient notification: notifications are PROJECTED from it at read time and
/// unread state is a per-member cursor (<see cref="Member.NotificationsSeenAt"/>).
///
/// It deliberately carries <b>no foreign key</b> to the entry/template it references — that
/// target may be hard-deleted — so the denormalized <see cref="PayloadJson"/> snapshot must
/// stand alone. <see cref="AffectedMemberIdsCsv"/> is the denormalized recipient set the read
/// projection filters on (the actor is never included).
/// </summary>
public class LedgerEvent
{
    public Guid Id { get; set; }

    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;

    /// <summary>Who performed the change.</summary>
    public Guid ActorMemberId { get; set; }

    public LedgerEventType Type { get; set; }

    /// <summary>Denormalized reference to the affected entry, if any (no FK — may be deleted).</summary>
    public Guid? EntryId { get; set; }

    /// <summary>Denormalized reference to the affected recurring template, if any (no FK).</summary>
    public Guid? RecurringTemplateId { get; set; }

    /// <summary>Members this event financially concerns, excluding the actor — comma-separated
    /// GUIDs. The read projection returns an event to a caller only if they appear here.</summary>
    public string AffectedMemberIdsCsv { get; set; } = "";

    /// <summary>Serialized <see cref="LedgerEventPayload"/>: the structured before/after of the
    /// changed fields plus the denormalized subject title and actor name.</summary>
    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>The structured, machine-readable body of a <see cref="LedgerEvent"/>. Values are
/// stored raw (amounts as minor units, split/cadence as wire strings, names denormalized) so
/// user-facing copy is rendered at read time and can change without rewriting history.</summary>
public sealed record LedgerEventPayload
{
    /// <summary>Denormalized subject title at event time (e.g. the entry/template title).</summary>
    public string Title { get; init; } = "";

    /// <summary>Denormalized actor display name — survives the actor later leaving.</summary>
    public string ActorName { get; init; } = "";

    /// <summary>Subject amount snapshot in minor units — context for a delete/settle.</summary>
    public long? AmountMinor { get; init; }

    /// <summary>Per-field before/after for an edit; null/empty for delete and settle.</summary>
    public List<LedgerFieldChange> Changes { get; init; } = new();
}

/// <summary>One changed field. <see cref="Field"/> is a stable key ("amount", "payer", "split",
/// "cadence", "date"); <see cref="Before"/>/<see cref="After"/> are raw values (minor units for
/// amount, member name for payer, wire string for split/cadence, ISO date for date).</summary>
public sealed record LedgerFieldChange(string Field, string? Before, string? After);
