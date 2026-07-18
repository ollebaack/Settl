using Settl.Api.Domain;

namespace Settl.Api.Services;

/// <summary>
/// Keyword → category inference from an entry title (Swedish keywords, first match wins —
/// order matters, e.g. <c>Cleaning</c> before <c>Groceries</c> so "Städmaterial" doesn't
/// match groceries). Mirrors the design addendum's client-side prototype, now the single
/// server-side source (entry-categories spec).
/// </summary>
public static class CategoryClassifier
{
    private static readonly (EntryCategory Category, string[] Keywords)[] Rules =
    [
        (EntryCategory.Cleaning, ["städ", "rengör", "tvätt"]),
        (EntryCategory.Restaurant, ["takeaway", "thai", "pizza", "sushi", "restaurang", "lunch", "middag", "käk"]),
        (EntryCategory.Event, ["konsert", "biljett", "bio", "match", "event"]),
        (EntryCategory.Furniture, ["soffa", "möbel", "stol", "bord", "säng", "fåtölj"]),
        (EntryCategory.Groceries, ["mat", "handl", "ica", "coop", "willys", "lidl", "hemköp"]),
        (EntryCategory.Transport, ["taxi", "buss", "tåg", "resa", "bensin", "parkering"]),
        (EntryCategory.Internet, ["internet", "wifi", "bredband"]),
        (EntryCategory.Rent, ["hyra"]),
        (EntryCategory.Music, ["spotify", "musik"]),
        (EntryCategory.Streaming, ["netflix", "hbo", "stream", "film", "tv"]),
        (EntryCategory.Electricity, ["el ", "elräkning", "ström"]),
        (EntryCategory.Gift, ["blommor", "present", "gåva", "årsdag"]),
    ];

    public static EntryCategory Classify(string title)
    {
        var lower = title.ToLowerInvariant();
        foreach (var (category, keywords) in Rules)
            if (keywords.Any(lower.Contains))
                return category;
        return EntryCategory.Other;
    }
}
