namespace Settl.Api.Dtos;

public sealed record HouseholdListItemDto(
    Guid Id,
    string Name,
    string Currency,
    IReadOnlyList<string> MemberNames,
    long NetMinor,
    string NetLabel);

/// <summary>
/// The acting user is always a member and never needs to be listed. <c>MemberIds</c>
/// references existing members; <c>NewMemberNames</c> creates fresh ones inline (no
/// invite step — auth is deferred, ADR-0005, so a Member is just a name).
/// </summary>
public sealed record CreateHouseholdRequest(
    string Name,
    string? Currency,
    Guid[]? MemberIds,
    string[]? NewMemberNames);

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
