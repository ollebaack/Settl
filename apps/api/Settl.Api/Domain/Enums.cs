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
