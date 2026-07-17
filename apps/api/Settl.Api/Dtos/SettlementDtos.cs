namespace Settl.Api.Dtos;

public sealed record SettleEntryDto(
    Guid Id,
    string Title,
    DateOnly Date,
    long SignedAmountMinor);

/// <summary>
/// A ready-to-open Swish pre-fill link for the acting debtor to pay the creditor
/// (swish-settlement-payments spec). Surfaced on <see cref="SettlePreviewDto.SwishPay"/> ONLY when
/// the acting user owes (net &lt; 0), the household currency is SEK, and the creditor has saved a
/// Swish number. Built server-side (ADR-0006); the amount is locked (no <c>edit</c> param). Settl
/// never learns whether the payment happened — settling stays the separate manual action.
/// </summary>
public sealed record SwishPayDto(string Uri, long AmountMinor);

public sealed record SettlePreviewDto(
    long NetMinor,
    string NetLabel,
    string MemberName,
    IReadOnlyList<SettleEntryDto> Entries,
    SwishPayDto? SwishPay);

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
