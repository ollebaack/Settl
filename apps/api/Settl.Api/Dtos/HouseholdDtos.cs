namespace Settl.Api.Dtos;

public sealed record HouseholdListItemDto(
    Guid Id,
    string Name,
    string Currency,
    IReadOnlyList<string> MemberNames,
    long NetMinor,
    string NetLabel);

/// <summary>
/// Creates a household with the acting user as its sole initial member. Everyone else
/// joins via invite (ADR-0011) — see <c>POST /households/{id}/invites</c>.
/// </summary>
public sealed record CreateHouseholdRequest(
    string Name,
    string? Currency);

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
