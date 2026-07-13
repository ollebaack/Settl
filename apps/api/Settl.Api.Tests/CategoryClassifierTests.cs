using Settl.Api.Domain;
using Settl.Api.Services;

namespace Settl.Api.Tests;

/// <summary>Pure unit tests for the keyword→category map (docs/specs/entry-categories.md).</summary>
public class CategoryClassifierTests
{
    [Theory]
    [InlineData("Städning", EntryCategory.Cleaning)]
    [InlineData("Thai takeaway", EntryCategory.Restaurant)]
    [InlineData("Konsertbiljett", EntryCategory.Event)]
    [InlineData("Ny soffa", EntryCategory.Furniture)]
    [InlineData("ICA storhandling", EntryCategory.Groceries)]
    [InlineData("Taxi hem", EntryCategory.Transport)]
    [InlineData("Bredband", EntryCategory.Internet)]
    [InlineData("Hyra — juli", EntryCategory.Rent)]
    [InlineData("Spotify", EntryCategory.Music)]
    [InlineData("Netflix", EntryCategory.Streaming)]
    [InlineData("Elräkning", EntryCategory.Electricity)]
    [InlineData("Blommor till Sam", EntryCategory.Gift)]
    [InlineData("Betalning för konsulttjänst", EntryCategory.Other)]
    public void Classify_matches_expected_category(string title, EntryCategory expected) =>
        Assert.Equal(expected, CategoryClassifier.Classify(title));

    [Fact]
    public void Classify_prefers_cleaning_over_groceries_for_stadmaterial() =>
        // "Städmaterial" contains both "städ" (Cleaning) and "mat" (Groceries) — order matters.
        Assert.Equal(EntryCategory.Cleaning, CategoryClassifier.Classify("Städmaterial"));

    [Fact]
    public void Classify_is_case_insensitive() =>
        Assert.Equal(EntryCategory.Rent, CategoryClassifier.Classify("HYRA"));
}
