using Settl.Api.Domain;

namespace Settl.Api.Dtos;

/// <summary>Maps domain enums to/from the wire strings named in the API contract.</summary>
public static class Contract
{
    public static string EntryType(EntryType t) => t switch
    {
        Domain.EntryType.Expense => "expense",
        Domain.EntryType.RecurringPost => "recurringPost",
        _ => throw new ArgumentOutOfRangeException(nameof(t))
    };

    public static string SplitMode(SplitMode m) => m switch
    {
        Domain.SplitMode.Equal => "equal",
        Domain.SplitMode.Percent => "percent",
        Domain.SplitMode.Amount => "amount",
        Domain.SplitMode.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(m))
    };

    public static SplitMode ParseSplitMode(string? mode) => mode?.Trim().ToLowerInvariant() switch
    {
        "equal" => Domain.SplitMode.Equal,
        "percent" => Domain.SplitMode.Percent,
        "amount" => Domain.SplitMode.Amount,
        "none" => Domain.SplitMode.None,
        _ => throw new SplitValidationException("Ogiltigt delningsläge")
    };

    public static string Cadence(Cadence c) => c switch
    {
        Domain.Cadence.Monthly => "monthly",
        Domain.Cadence.Biweekly => "biweekly",
        Domain.Cadence.Weekly => "weekly",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static Cadence ParseCadence(string? cadence) => cadence?.Trim().ToLowerInvariant() switch
    {
        "monthly" => Domain.Cadence.Monthly,
        "biweekly" or "2weeks" => Domain.Cadence.Biweekly,
        "weekly" => Domain.Cadence.Weekly,
        _ => throw new SplitValidationException("Ogiltig kadens")
    };

    public static string EntryCategory(EntryCategory c) => c switch
    {
        Domain.EntryCategory.Cleaning => "cleaning",
        Domain.EntryCategory.Restaurant => "restaurant",
        Domain.EntryCategory.Event => "event",
        Domain.EntryCategory.Furniture => "furniture",
        Domain.EntryCategory.Groceries => "groceries",
        Domain.EntryCategory.Transport => "transport",
        Domain.EntryCategory.Internet => "internet",
        Domain.EntryCategory.Rent => "rent",
        Domain.EntryCategory.Music => "music",
        Domain.EntryCategory.Streaming => "streaming",
        Domain.EntryCategory.Electricity => "electricity",
        Domain.EntryCategory.Gift => "gift",
        Domain.EntryCategory.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static EntryCategory ParseEntryCategory(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "cleaning" => Domain.EntryCategory.Cleaning,
        "restaurant" => Domain.EntryCategory.Restaurant,
        "event" => Domain.EntryCategory.Event,
        "furniture" => Domain.EntryCategory.Furniture,
        "groceries" => Domain.EntryCategory.Groceries,
        "transport" => Domain.EntryCategory.Transport,
        "internet" => Domain.EntryCategory.Internet,
        "rent" => Domain.EntryCategory.Rent,
        "music" => Domain.EntryCategory.Music,
        "streaming" => Domain.EntryCategory.Streaming,
        "electricity" => Domain.EntryCategory.Electricity,
        "gift" => Domain.EntryCategory.Gift,
        "other" => Domain.EntryCategory.Other,
        _ => throw new SplitValidationException("Ogiltig kategori")
    };

    public static string InviteChannel(InviteChannel c) => c switch
    {
        Domain.InviteChannel.Email => "email",
        Domain.InviteChannel.Sms => "sms",
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    public static InviteChannel ParseInviteChannel(string? channel) => channel?.Trim().ToLowerInvariant() switch
    {
        "email" => Domain.InviteChannel.Email,
        "sms" => Domain.InviteChannel.Sms,
        _ => throw new SplitValidationException("Ogiltig inbjudningskanal")
    };

    public static string NudgeTone(NudgeTone t) => t switch
    {
        Domain.NudgeTone.Gentle => "gentle",
        Domain.NudgeTone.Direct => "direct",
        _ => throw new ArgumentOutOfRangeException(nameof(t))
    };

    /// <summary>Parses the wire tone. Returns null (not throwing) for unknown values so the
    /// caller can turn it into a 400 with its own message — same shape as the other parsers.</summary>
    public static NudgeTone? TryParseNudgeTone(string? tone) => tone?.Trim().ToLowerInvariant() switch
    {
        "gentle" => Domain.NudgeTone.Gentle,
        "direct" => Domain.NudgeTone.Direct,
        _ => null
    };

    public static string ViewerStatusKind(ViewerStatusKind k) => k switch
    {
        Domain.ViewerStatusKind.Settled => "settled",
        Domain.ViewerStatusKind.YouOwe => "youOwe",
        Domain.ViewerStatusKind.YouAreOwed => "youAreOwed",
        Domain.ViewerStatusKind.PartiallySettled => "partiallySettled",
        Domain.ViewerStatusKind.NotYourShare => "notYourShare",
        _ => throw new ArgumentOutOfRangeException(nameof(k))
    };
}
