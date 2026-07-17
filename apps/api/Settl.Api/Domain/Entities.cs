using Microsoft.AspNetCore.Identity;

namespace Settl.Api.Domain;

/// <summary>A person. Also the ASP.NET Identity user (ADR-0011) — login credentials
/// (Email, PasswordHash, ...) come from <see cref="IdentityUser{TKey}"/>.</summary>
public class Member : IdentityUser<Guid>
{
    public string Name { get; set; } = "";

    /// <summary>Hex avatar colour — member data, NOT a UI token (e.g. <c>#dfe6cf</c>).</summary>
    public string AvatarColor { get; set; } = "";

    /// <summary>Optional emoji shown on <see cref="AvatarColor"/> in place of the letter
    /// <see cref="Initial"/> (ADR-0019). Null = fall back to the initial. Untrusted text:
    /// validated to a single emoji grapheme on write (it renders in other members' UIs).</summary>
    public string? AvatarEmoji { get; set; }

    /// <summary>Optional Swish number this member wants settlement payments sent to
    /// (swish-settlement-payments spec). Stored E.164 like the inherited
    /// <see cref="IdentityUser{TKey}.PhoneNumber"/> but DELIBERATELY DISTINCT from it — a Swish
    /// number isn't necessarily the account phone, and neither is derived from the other. Same
    /// trust posture as the profile phone: unverified contact data (tech-debt/0010), never a
    /// lookup key or auth factor. Null = opted out (no "Betala med Swish" action shown).</summary>
    public string? SwishNumber { get; set; }

    /// <summary>The member's chosen nudge voice (implementation-map §2.4, ambiguity #18).
    /// Defaults to <see cref="Domain.NudgeTone.Direct"/> — the tone picked as the product default,
    /// now exposed as a per-user setting. Selects nudge copy only, never which nudges fire.</summary>
    public NudgeTone NudgeTone { get; set; } = NudgeTone.Direct;

    /// <summary>Whether this member receives the daily nudge-digest email (reminder-delivery spec,
    /// ADR-0024). Off by default — an explicit opt-in the member turns on with the profile switch;
    /// the one-click email unsubscribe also forces it off. In-app nudges are unaffected.</summary>
    public bool NudgeEmailsEnabled { get; set; }

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

    /// <summary>The single owner (ADR-0016). Always one of this household's current members —
    /// backfilled to the earliest-<see cref="HouseholdMembership.JoinedAt"/> member for rows
    /// created before ownership was recorded.</summary>
    public Guid OwnerMemberId { get; set; }

    /// <summary>Soft-archive marker (ADR-0016). Null = active; set = archived (hidden but
    /// fully retained and restorable by the owner). Never hard-deleted.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

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
    public EntryCategory Category { get; set; } = EntryCategory.Other;
    public long AmountMinor { get; set; }
    public DateOnly Date { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Guid? PaidByMemberId { get; set; }

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
    public EntryCategory Category { get; set; } = EntryCategory.Other;
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

/// <summary>
/// An invite (ADR-0011 email, ADR-0019 SMS). No Member/HouseholdMembership row exists for
/// the invitee until <see cref="AcceptedAt"/> is set — activation happens entirely through
/// the emailed/texted link. Only <see cref="TokenHash"/> is persisted; the raw token lives
/// in the link and is never stored.
///
/// An invite may be <b>household-scoped</b> (<see cref="HouseholdId"/> set → accepting also
/// joins that household) or <b>contact-only</b> (null → accepting just creates a
/// <see cref="Contact"/> edge). Either way, accepting proves consent and, for SMS, ownership
/// of the number, which is why a self-entered profile phone needs no OTP (tech-debt/0010).
/// </summary>
public class Invite
{
    public Guid Id { get; set; }

    /// <summary>Null for a contact-only invite (ADR-0019) — no household is joined on accept.</summary>
    public Guid? HouseholdId { get; set; }
    public Household? Household { get; set; }

    public InviteChannel Channel { get; set; } = InviteChannel.Email;

    /// <summary>Normalized (lowercase, trimmed) invitee email. Null for SMS invites.</summary>
    public string? Email { get; set; }

    /// <summary>E.164 invitee phone for SMS invites (ADR-0019). This raw typed number is
    /// transient third-party data: it is scrubbed on accept and once the invite expires, so
    /// the persistent graph only ever holds relationships between consenting members.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>SHA-256 hash of the raw token embedded in the accept link.</summary>
    public string TokenHash { get; set; } = "";

    public Guid InvitedByMemberId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

/// <summary>
/// A directed contact edge between two members (ADR-0019). Created only when an invite is
/// accepted — connection-on-accept — so the graph never holds scraped or unconsented numbers.
/// Edges are created in both directions on accept, and are reusable across households to
/// pre-fill future invites (the wishlist payoff). There is deliberately no friend-request,
/// blocking, or presence concept.
/// </summary>
public class Contact
{
    /// <summary>The member who owns this contact-list row.</summary>
    public Guid OwnerMemberId { get; set; }
    public Member OwnerMember { get; set; } = null!;

    /// <summary>The other person, saved in <see cref="OwnerMemberId"/>'s contacts.</summary>
    public Guid ContactMemberId { get; set; }
    public Member ContactMember { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// One row per nudge already emailed to a member — the persisted, de-duplicated delivery record
/// the reminder-delivery spec (ADR-0024) introduces to pay down tech-debt/0002. It is a
/// delivery-dedup log, NOT a mirror of nudge state: <see cref="NudgeKey"/> is a stable identity
/// derived entirely from the nudge's own fields (<see cref="NudgeCalculator.EmittableNudge.Key"/>),
/// so the daily digest can ask "have we already emailed this?" without coordinating with the
/// derive-on-read crossing logic (ADR-0023). A standing condition keeps one key and is emailed
/// once; a fresh crossing / new cycle yields a new key and re-fires.
/// </summary>
public class EmittedNudge
{
    public Guid Id { get; set; }

    /// <summary>The recipient — who was emailed. Uniqueness is per (member, key).</summary>
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;

    /// <summary>The derivable nudge identity (e.g. <c>balance:{memberId}:{crossedOn}</c>).</summary>
    public string NudgeKey { get; set; } = "";

    /// <summary>When the digest carrying this nudge was sent (UTC).</summary>
    public DateTimeOffset SentAt { get; set; }
}
