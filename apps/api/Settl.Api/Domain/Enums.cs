namespace Settl.Api.Domain;

public enum EntryType
{
    Expense,
    RecurringPost
}

public enum SplitMode
{
    Equal,
    Percent,
    Amount,
    None
}

public enum Cadence
{
    Monthly,
    Biweekly,
    Weekly
}

/// <summary>How an <see cref="Invite"/> is delivered (contacts-phone-sms spec). Email is the original
/// channel (ADR-0005); SMS is a blind invite to a typed phone number — never a lookup.</summary>
public enum InviteChannel
{
    Email,
    Sms
}

/// <summary>A member's chosen reminder/nudge voice (implementation-map §2.4, ambiguity #18).
/// <c>Direct</c> is the default (states the amount and asks to settle); <c>Gentle</c> softens
/// the same nudges. The tone only selects copy — it never changes which nudges fire.
/// <c>Direct</c> is deliberately the zero value so the CLR default matches the DB default
/// (avoids EF's database-generated-default sentinel ambiguity).</summary>
public enum NudgeTone
{
    Direct,
    Gentle
}

/// <summary>Applies to all entry types, but only <c>Expense</c> rows use it for icon
/// selection today (entry-categories spec) — assigned server-side from the title via
/// <see cref="Settl.Api.Services.CategoryClassifier"/>, user-overridable afterward.</summary>
public enum EntryCategory
{
    Cleaning,
    Restaurant,
    Event,
    Furniture,
    Groceries,
    Transport,
    Internet,
    Rent,
    Music,
    Streaming,
    Electricity,
    Gift,
    Other
}
