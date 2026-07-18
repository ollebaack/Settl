namespace Settl.Api.Dtos;

/// <summary>One changed field on an <c>entryEdited</c>/<c>recurringChanged</c> notification.
/// <see cref="Label"/> and the rendered <see cref="Before"/>/<see cref="After"/> are
/// display-ready sv-SE strings — the web renders them verbatim (ADR-0006).</summary>
public sealed record NotificationChangeDto(string Field, string Label, string? Before, string? After);

/// <summary>A trust notification projected from a <c>LedgerEvent</c> for the calling member
/// (trust-notifications-v1). <see cref="IsUnread"/> is computed against the member's read
/// cursor at request time.</summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    Guid ActorMemberId,
    string ActorName,
    string Title,
    long? AmountMinor,
    Guid? EntryId,
    Guid? RecurringTemplateId,
    IReadOnlyList<NotificationChangeDto> Changes,
    DateTimeOffset OccurredAt,
    bool IsUnread);

public sealed record NotificationListDto(int UnreadCount, IReadOnlyList<NotificationDto> Items);
