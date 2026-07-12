using Microsoft.EntityFrameworkCore;
using Settl.Api.Domain;

namespace Settl.Api.Data;

/// <summary>
/// Seeds dev data when the DB is empty, mirroring the canonical fixture in
/// docs/design/"Settl App".dc.html (amounts ×100 into öre). All dates are RELATIVE to
/// DateTime.UtcNow so nudges and cycle progress stay live. Guarantees live data for all
/// three nudge types (recurring due ≤5 days, ≥1500 kr expense, a pair net ≥750 kr).
/// </summary>
public static class DbInitializer
{
    // Stable ids live in SeedIds so tests can reach seeded rows. Aliased here to keep the
    // seed body terse; values and behaviour are unchanged.
    private static readonly Guid Du = SeedIds.Du;
    private static readonly Guid Sam = SeedIds.Sam;
    private static readonly Guid Priya = SeedIds.Priya;
    private static readonly Guid Mamma = SeedIds.Mamma;
    private static readonly Guid Pappa = SeedIds.Pappa;

    private static readonly Guid Lonnvagen = SeedIds.Lonnvagen;
    private static readonly Guid Familjen = SeedIds.Familjen;

    private static readonly Guid Rent = SeedIds.Rent;
    private static readonly Guid Cleaning = SeedIds.Cleaning;
    private static readonly Guid Spotify = SeedIds.Spotify;
    private static readonly Guid Internet = SeedIds.Internet;
    private static readonly Guid Netflix = SeedIds.Netflix;

    public static async Task SeedAsync(SettlDbContext db, CancellationToken ct = default)
    {
        if (await db.Members.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        DateOnly D(int offset) => today.AddDays(offset);

        // Members
        db.Members.AddRange(
            new Member { Id = Du, Name = "Du", AvatarColor = "#dfe6cf" },
            new Member { Id = Sam, Name = "Sam", AvatarColor = "#f0dcc3" },
            new Member { Id = Priya, Name = "Priya", AvatarColor = "#d9e0ee" },
            new Member { Id = Mamma, Name = "Mamma", AvatarColor = "#eed9d9" },
            new Member { Id = Pappa, Name = "Pappa", AvatarColor = "#d9eee4" });

        // Households + memberships (JoinedAt increasing → membership order matches list order).
        var h1Order = new[] { Du, Sam, Priya };
        var h2Order = new[] { Du, Mamma, Pappa };

        db.Households.AddRange(
            new Household { Id = Lonnvagen, Name = "Lönnvägen 3", Currency = "SEK", CreatedAt = now },
            new Household { Id = Familjen, Name = "Familjen", Currency = "SEK", CreatedAt = now });

        for (var i = 0; i < h1Order.Length; i++)
            db.HouseholdMemberships.Add(new HouseholdMembership
            { HouseholdId = Lonnvagen, MemberId = h1Order[i], JoinedAt = now.AddSeconds(i) });
        for (var i = 0; i < h2Order.Length; i++)
            db.HouseholdMemberships.Add(new HouseholdMembership
            { HouseholdId = Familjen, MemberId = h2Order[i], JoinedAt = now.AddSeconds(i) });

        // ---- Entries (Lönnvägen 3) ----
        var e8 = Expense(Lonnvagen, "Begagnad soffa", 240_000, Sam, SplitMode.Percent, h1Order,
            new() { [Du] = 40m, [Sam] = 40m, [Priya] = 20m }, D(-1), now);
        var e1 = Expense(Lonnvagen, "Matinköp — storhandling", 86_400, Sam, SplitMode.Equal, h1Order, new(), D(-2), now);
        var e2 = Iou(Lonnvagen, "Konsertbiljett", 20_000, Du, Priya, D(-3), now);
        var e5 = Expense(Lonnvagen, "Thai takeaway", 54_000, Priya, SplitMode.Equal, h1Order, new(), D(-6), now);
        var e6 = Expense(Lonnvagen, "Städmaterial", 24_900, Du, SplitMode.Equal, h1Order, new(), D(-10), now);
        var e4 = RecurringPost(Lonnvagen, "Internet — juli", 44_900, Du, SplitMode.Equal, h1Order, new(), D(-11), now, Internet);
        var e10 = Iou(Lonnvagen, "Taxi hem", 12_000, Sam, Du, D(-12), now);
        var e9 = RecurringPost(Lonnvagen, "Spotify Family — juni", 16_900, Sam, SplitMode.Equal, h1Order, new(), D(-14), now, Spotify);
        var e3 = RecurringPost(Lonnvagen, "Hyra — juli", 2_400_000, Du, SplitMode.Amount, h1Order,
            new() { [Du] = 900_000m, [Sam] = 800_000m, [Priya] = 700_000m }, D(-27), now, Rent);

        // ---- Entries (Familjen) ----
        var e20 = Expense(Familjen, "Blommor på årsdagen", 45_000, Pappa, SplitMode.Equal, h2Order, new(), D(-4), now);
        var e21 = RecurringPost(Familjen, "Netflix — juli", 22_900, Du, SplitMode.Equal, h2Order, new(), D(-9), now, Netflix);

        db.Entries.AddRange(e8, e1, e2, e5, e6, e4, e10, e9, e3, e20, e21);

        // ---- Recurring templates ----
        db.RecurringTemplates.AddRange(
            Template(Rent, Lonnvagen, "Hyra", 2_400_000, Cadence.Monthly, D(3), Du, SplitMode.Amount, h1Order,
                new() { [Du] = 900_000m, [Sam] = 800_000m, [Priya] = 700_000m }, now),
            Template(Cleaning, Lonnvagen, "Städhjälp", 120_000, Cadence.Biweekly, D(6), Priya, SplitMode.Equal, h1Order, new(), now),
            Template(Spotify, Lonnvagen, "Spotify Family", 16_900, Cadence.Monthly, D(16), Sam, SplitMode.Equal, h1Order, new(), now),
            Template(Internet, Lonnvagen, "Internet", 44_900, Cadence.Monthly, D(20), Du, SplitMode.Equal, h1Order, new(), now),
            Template(Netflix, Familjen, "Netflix", 22_900, Cadence.Monthly, D(22), Du, SplitMode.Equal, h2Order, new(), now));

        // ---- Settlements: close all debts of the two settled entries (e6, e3) ----
        var settlement = new Settlement
        {
            Id = Guid.NewGuid(),
            HouseholdId = Lonnvagen,
            SettledAt = now.AddDays(-9),
            InitiatedByMemberId = Du
        };
        foreach (var e in new[] { e6, e3 })
            foreach (var d in BalanceCalculator.Debts(e))
                settlement.Closures.Add(new SettlementClosure
                {
                    Id = Guid.NewGuid(),
                    EntryId = e.Id,
                    DebtorMemberId = d.Debtor,
                    CreditorMemberId = d.Creditor
                });
        db.Settlements.Add(settlement);

        await db.SaveChangesAsync(ct);
    }

    private static Entry Expense(Guid hh, string title, long amount, Guid paidBy, SplitMode mode,
        IReadOnlyList<Guid> order, Dictionary<Guid, decimal> formula, DateOnly date, DateTimeOffset now) =>
        BuildEntry(EntryType.Expense, hh, title, amount, paidBy, mode, order, formula, date, now, null);

    private static Entry RecurringPost(Guid hh, string title, long amount, Guid paidBy, SplitMode mode,
        IReadOnlyList<Guid> order, Dictionary<Guid, decimal> formula, DateOnly date, DateTimeOffset now, Guid templateId) =>
        BuildEntry(EntryType.RecurringPost, hh, title, amount, paidBy, mode, order, formula, date, now, templateId);

    private static Entry BuildEntry(EntryType type, Guid hh, string title, long amount, Guid paidBy, SplitMode mode,
        IReadOnlyList<Guid> order, Dictionary<Guid, decimal> formula, DateOnly date, DateTimeOffset now, Guid? templateId)
    {
        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            HouseholdId = hh,
            Type = type,
            Title = title,
            AmountMinor = amount,
            Date = date,
            CreatedAt = now,
            PaidByMemberId = paidBy,
            SplitMode = mode,
            RecurringTemplateId = templateId
        };
        foreach (var s in ShareFreezer.Freeze(mode, order, amount, formula))
            entry.Shares.Add(new EntryShare
            { EntryId = entry.Id, MemberId = s.MemberId, ShareMinor = s.ShareMinor, FormulaValue = s.FormulaValue });
        return entry;
    }

    private static Entry Iou(Guid hh, string title, long amount, Guid from, Guid to, DateOnly date, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            HouseholdId = hh,
            Type = EntryType.Iou,
            Title = title,
            AmountMinor = amount,
            Date = date,
            CreatedAt = now,
            FromMemberId = from,
            ToMemberId = to,
            SplitMode = SplitMode.None
        };

    private static RecurringTemplate Template(Guid id, Guid hh, string title, long amount, Cadence cadence,
        DateOnly next, Guid paidBy, SplitMode mode, IReadOnlyList<Guid> order, Dictionary<Guid, decimal> formula, DateTimeOffset now)
    {
        var t = new RecurringTemplate
        {
            Id = id,
            HouseholdId = hh,
            Title = title,
            AmountMinor = amount,
            Cadence = cadence,
            NextPostDate = next,
            PaidByMemberId = paidBy,
            SplitMode = mode,
            Active = true,
            CreatedAt = now
        };
        foreach (var m in order)
            t.Shares.Add(new RecurringShare
            {
                RecurringTemplateId = id,
                MemberId = m,
                FormulaValue = mode == SplitMode.Equal ? null : formula.TryGetValue(m, out var v) ? v : 0m
            });
        return t;
    }
}
