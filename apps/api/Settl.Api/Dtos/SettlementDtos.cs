namespace Settl.Api.Dtos;

public sealed record SettleEntryDto(
    Guid Id,
    string Title,
    DateOnly Date,
    long SignedAmountMinor);

public sealed record SettlePreviewDto(
    long NetMinor,
    string NetLabel,
    string MemberName,
    IReadOnlyList<SettleEntryDto> Entries);

public sealed record CreateSettlementRequest(Guid PersonMemberId);

public sealed record CreateSettlementResponse(Guid SettlementId);

/// <summary>One entry closed by a settlement, for the pairwise history view.</summary>
public sealed record SettlementHistoryEntryDto(
    Guid Id,
    string Title,
    DateOnly Date,
    long AmountMinor);

/// <summary>
/// A past settlement event as seen by one pair (acting user ↔ person). <see cref="NetClearedMinor"/>
/// is signed toward the viewer (&gt;0 person owed you, &lt;0 you owed person); amounts are derived
/// from the closed entries' frozen shares (ADR-0006, ADR-0007), never stored on the closure.
/// </summary>
public sealed record SettlementHistoryItemDto(
    Guid Id,
    DateTimeOffset SettledAt,
    long NetClearedMinor,
    Guid InitiatedByMemberId,
    int ClosedEntryCount,
    IReadOnlyList<SettlementHistoryEntryDto> Entries);
