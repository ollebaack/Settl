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
