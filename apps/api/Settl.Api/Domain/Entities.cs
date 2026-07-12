namespace Settl.Api.Domain;

/// <summary>A person; global identity stub (auth deferred, ADR-0005).</summary>
public class Member
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary>Hex avatar colour — member data, NOT a UI token (e.g. <c>#dfe6cf</c>).</summary>
    public string AvatarColor { get; set; } = "";

    public ICollection<HouseholdMembership> Memberships { get; set; } = new List<HouseholdMembership>();

    /// <summary>Derived, not stored.</summary>
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}

public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "SEK";
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<HouseholdMembership> Memberships { get; set; } = new List<HouseholdMembership>();
    public ICollection<Entry> Entries { get; set; } = new List<Entry>();
    public ICollection<RecurringTemplate> RecurringTemplates { get; set; } = new List<RecurringTemplate>();
    public ICollection<Settlement> Settlements { get; set; } = new List<Settlement>();
}

/// <summary>Join entity — user↔household is MANY-TO-MANY.</summary>
public class HouseholdMembership
{
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; }
}

public class Entry
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public EntryType Type { get; set; }
    public string Title { get; set; } = "";
    public long AmountMinor { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Guid? PaidByMemberId { get; set; }
    public Guid? FromMemberId { get; set; }
    public Guid? ToMemberId { get; set; }

    public SplitMode SplitMode { get; set; }
    public Guid? RecurringTemplateId { get; set; }
    public RecurringTemplate? RecurringTemplate { get; set; }

    public ICollection<EntryShare> Shares { get; set; } = new List<EntryShare>();
}

/// <summary>Frozen per-member share plus the formula input that produced it.</summary>
public class EntryShare
{
    public Guid EntryId { get; set; }
    public Entry Entry { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    /// <summary>Frozen integer share, computed once at write time.</summary>
    public long ShareMinor { get; set; }

    /// <summary>Percent (e.g. 40) for Percent mode, minor units for Amount mode, null for Equal.</summary>
    public decimal? FormulaValue { get; set; }
}

public class RecurringTemplate
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Title { get; set; } = "";
    public long AmountMinor { get; set; }
    public Cadence Cadence { get; set; }
    public DateOnly NextPostDate { get; set; }
    public Guid PaidByMemberId { get; set; }
    public SplitMode SplitMode { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RecurringShare> Shares { get; set; } = new List<RecurringShare>();
    public ICollection<Entry> PostedEntries { get; set; } = new List<Entry>();
}

/// <summary>Template split formula; frozen shares are recomputed per posted cycle.</summary>
public class RecurringShare
{
    public Guid RecurringTemplateId { get; set; }
    public RecurringTemplate RecurringTemplate { get; set; } = null!;
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    /// <summary>Percent or minor units per member; null for Equal.</summary>
    public decimal? FormulaValue { get; set; }
}

/// <summary>First-class settlement event. Settled state is derived from these, never a flag.</summary>
public class Settlement
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public DateTimeOffset SettledAt { get; set; }
    public Guid InitiatedByMemberId { get; set; }

    public ICollection<SettlementClosure> Closures { get; set; } = new List<SettlementClosure>();
}

/// <summary>Closes ONE debt within ONE entry.</summary>
public class SettlementClosure
{
    public Guid Id { get; set; }
    public Guid SettlementId { get; set; }
    public Settlement Settlement { get; set; } = null!;
    public Guid EntryId { get; set; }
    public Entry Entry { get; set; } = null!;

    /// <summary>The debt: Debtor owes Creditor. Stored in the real direction.</summary>
    public Guid DebtorMemberId { get; set; }
    public Guid CreditorMemberId { get; set; }
}
