namespace Settl.Api.Dtos;

public sealed record HouseholdListItemDto(
    Guid Id,
    string Name,
    string Currency,
    IReadOnlyList<string> MemberNames,
    long NetMinor,
    string NetLabel);

public sealed record CreateHouseholdRequest(
    string Name,
    string? Currency,
    Guid[] MemberIds);

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    string Currency,
    IReadOnlyList<MemberDto> Members);

public sealed record PersonBalanceDto(
    Guid MemberId,
    string Name,
    string AvatarColor,
    long NetMinor,
    string Relation);

public sealed record UpcomingDto(
    Guid RecurringId,
    string Title,
    DateOnly NextPostDate,
    int DaysUntil,
    long YourShareMinor,
    long AmountMinor);

public sealed record HouseholdSummaryDto(
    long OverallNetMinor,
    string NetLabel,
    int OpenCount,
    IReadOnlyList<PersonBalanceDto> People,
    IReadOnlyList<UpcomingDto> Upcoming);
