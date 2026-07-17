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

/// <summary>How an <see cref="Invite"/> is delivered (ADR-0019). Email is the original
/// channel (ADR-0011); SMS is a blind invite to a typed phone number — never a lookup.</summary>
public enum InviteChannel
{
    Email,
    Sms
}

/// <summary>Applies to all entry types, but only <c>Expense</c> rows use it for icon
/// selection today (ADR-0012) — assigned server-side from the title via
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
