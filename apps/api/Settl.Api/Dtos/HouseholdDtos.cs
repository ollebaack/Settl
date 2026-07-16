namespace Settl.Api.Dtos;

public sealed record HouseholdListItemDto(
    Guid Id,
    string Name,
    string Currency,
    IReadOnlyList<string> MemberNames,
    long NetMinor,
    string NetLabel,
    Guid OwnerMemberId,
    bool IsOwner,
    DateTimeOffset? ArchivedAt);

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
    IReadOnlyList<MemberDto> Members,
    Guid OwnerMemberId,
    bool IsOwner,
    DateTimeOffset? ArchivedAt);

/// <summary>Reassigns ownership to another current member (owner-only). ADR-0016.</summary>
public sealed record TransferOwnershipRequest(Guid NewOwnerMemberId);

/// <summary>
/// Result of leaving a household. <see cref="Archived"/> is true only for the sole-owner
/// case, where leaving archives the household (and keeps the caller as owner) instead of
/// removing a membership (ADR-0016) — the client uses it to move the household into
/// "Arkiverade" rather than dropping it from the list.
/// </summary>
public sealed record LeaveResultDto(bool Archived);

/// <summary>
/// Everything the leave/archive confirmation sheets need, in one call (ADR-0016).
/// <see cref="ViewerOpenDebts"/> drives the per-person leave warning; <see cref="HouseholdOpenTotalMinor"/>
/// drives the household-wide archive warning. Debts warn but never block.
/// </summary>
public sealed record RemovalPreviewDto(
    bool IsOwner,
    int MemberCount,
    bool SoleMember,
    bool MustTransferFirst,
    IReadOnlyList<PersonBalanceDto> ViewerOpenDebts,
    long HouseholdOpenTotalMinor);

public sealed record PersonBalanceDto(
    Guid MemberId,
    string Name,
    string AvatarColor,
    string? AvatarEmoji,
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
