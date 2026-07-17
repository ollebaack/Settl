namespace Settl.Api.Dtos;

public sealed record RecurringDto(
    Guid Id,
    string Title,
    long AmountMinor,
    string Cadence,
    DateOnly NextPostDate,
    int DaysUntil,
    bool Active,
    string PayerName,
    string SplitMode,
    long YourShareMinor,
    long MonthlyNormalizedMinor,
    double CycleProgress,
    IReadOnlyList<Guid> ContributingMemberIds,
    DateOnly? EndDate,
    bool Ended);

public sealed record RecurringListDto(
    long RecTotalMinor,
    long RecShareMinor,
    IReadOnlyList<RecurringDto> Templates);

public sealed record RecurringShareRowDto(
    Guid MemberId,
    string Name,
    long ShareMinor,
    bool IsPayer);

public sealed record PostedEntrySummaryDto(
    Guid Id,
    string Title,
    long AmountMinor,
    bool Settled);

public sealed record RecurringDetailDto(
    RecurringDto Template,
    IReadOnlyList<RecurringShareRowDto> Shares,
    IReadOnlyList<PostedEntrySummaryDto> PostedEntries);

public sealed record CreateRecurringRequest(
    string? Title,
    long AmountMinor,
    string Cadence,
    DateOnly NextPostDate,
    Guid PaidByMemberId,
    SplitInput Split,
    // Termination (recurring-end-date spec). One mutually-exclusive mode resolved to a single
    // EndDate server-side: "never"/null → no end; "date" → uses EndDate; "count" → resolves
    // EndAfterCount to the Nth post date. Only the resolved date is stored.
    string? EndMode = null,
    DateOnly? EndDate = null,
    int? EndAfterCount = null);

public sealed record UpdateRecurringRequest(
    bool? Active,
    string? Title,
    long? AmountMinor,
    string? Cadence,
    DateOnly? NextPostDate,
    Guid? PaidByMemberId,
    SplitInput? Split,
    // Null EndMode leaves the end date unchanged; "never" clears it, "date"/"count" set it.
    string? EndMode = null,
    DateOnly? EndDate = null,
    int? EndAfterCount = null);
