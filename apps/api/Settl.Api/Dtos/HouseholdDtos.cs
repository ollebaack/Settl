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
/// joins via invite (ADR-0005) — see <c>POST /households/{id}/invites</c>.
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

/// <summary>Reassigns ownership to another current member (owner-only). See the household-ownership spec.</summary>
public sealed record TransferOwnershipRequest(Guid NewOwnerMemberId);

/// <summary>
/// Result of leaving a household. <see cref="Archived"/> is true only for the sole-owner
/// case, where leaving archives the household (and keeps the caller as owner) instead of
/// removing a membership (household-ownership spec) — the client uses it to move the household into
/// "Arkiverade" rather than dropping it from the list.
/// </summary>
public sealed record LeaveResultDto(bool Archived);

/// <summary>
/// Everything the leave/archive/delete confirmation sheets need, in one call
/// (household-ownership spec). <see cref="ViewerOpenDebts"/> drives the per-person leave warning;
/// <see cref="HouseholdOpenTotalMinor"/> drives the household-wide archive warning;
/// <see cref="IsEmpty"/> tells the client whether the owner may hard-delete (no entries,
/// recurring templates, or settlements). Debts warn but never block.
/// </summary>
public sealed record RemovalPreviewDto(
    bool IsOwner,
    int MemberCount,
    bool SoleMember,
    bool MustTransferFirst,
    IReadOnlyList<PersonBalanceDto> ViewerOpenDebts,
    long HouseholdOpenTotalMinor,
    bool IsEmpty);

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

/// <summary>
/// Per-person "who paid how much, when" for a single household, bucketed by month
/// (docs/specs/household-statistics.md). Aggregated server-side per ADR-0006. Buckets
/// are a continuous, zero-filled month series so the chart axis has no gaps;
/// <see cref="Members"/> lists only the members with any contribution in range, each
/// appearing as one chart series. Money stays integer minor units.
/// </summary>
public sealed record ContributionStatsDto(
    string Currency,
    IReadOnlyList<ContributionMemberDto> Members,
    IReadOnlyList<ContributionBucketDto> Buckets);

public sealed record ContributionMemberDto(
    Guid MemberId,
    string Name,
    string AvatarColor,
    string? AvatarEmoji);

/// <summary><see cref="Month"/> is the bucket key as "yyyy-MM" (a stable axis label).</summary>
public sealed record ContributionBucketDto(
    string Month,
    IReadOnlyList<MemberContributionDto> PerMember);

public sealed record MemberContributionDto(
    Guid MemberId,
    long PaidMinor);
