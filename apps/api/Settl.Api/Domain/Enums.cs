namespace Settl.Api.Domain;

public enum EntryType
{
    Expense,
    Iou,
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
