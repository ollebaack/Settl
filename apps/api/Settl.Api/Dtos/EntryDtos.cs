namespace Settl.Api.Dtos;

public sealed record ShareDto(
    Guid MemberId,
    string Name,
    string AvatarColor,
    string? AvatarEmoji,
    long ShareMinor,
    bool IsPayer);

public sealed record ViewerStatusDto(string Kind, long AmountMinor);

public sealed record EntryDto(
    Guid Id,
    Guid HouseholdId,
    string Type,
    string Title,
    string Category,
    long AmountMinor,
    DateOnly Date,
    DateTimeOffset CreatedAt,
    Guid? PaidByMemberId,
    Guid? FromMemberId,
    Guid? ToMemberId,
    string SplitMode,
    IReadOnlyList<ShareDto> Shares,
    Guid? RecurringTemplateId,
    string? TemplateTitle,
    bool Settled,
    bool Locked,
    ViewerStatusDto ViewerStatus);

/// <summary>Per-member split formula input; values are percent (Percent) or minor units (Amount).</summary>
public sealed record SplitInput(string Mode, Dictionary<Guid, decimal>? Values);

public sealed record CreateEntryRequest(
    string Type,
    string? Title,
    long AmountMinor,
    DateOnly? Date,
    Guid? PaidByMemberId,
    Guid? FromMemberId,
    Guid? ToMemberId,
    SplitInput? Split);

public sealed record UpdateEntryRequest(
    string Type,
    string? Title,
    long AmountMinor,
    DateOnly? Date,
    Guid? PaidByMemberId,
    Guid? FromMemberId,
    Guid? ToMemberId,
    SplitInput? Split,
    string? Category);
