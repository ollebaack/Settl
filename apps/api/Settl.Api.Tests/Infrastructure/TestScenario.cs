using Microsoft.AspNetCore.Identity;
using Settl.Api.Data;
using Settl.Api.Domain;

namespace Settl.Api.Tests.Infrastructure;

/// <summary>
/// Minimal, typed builder for ad-hoc households when the full canonical seed
/// (<see cref="DbInitializer.SeedAsync"/>) is overkill. Builds members, ONE household with
/// membership order preserved, and simple entries, then persists via
/// <see cref="SettlApiFactory.SeedAsync(TestScenario)"/> or <see cref="SaveAsync"/>.
///
/// Money is integer minor units (öre). Dates are day offsets from UtcNow. Shares are frozen at
/// build time through the same <see cref="ShareFreezer"/> the API uses, so splits are realistic.
/// </summary>
public sealed class TestScenario
{
    private readonly Household _household;
    private readonly List<Member> _members = [];
    private readonly List<HouseholdMembership> _memberships = [];
    private readonly List<Entry> _entries = [];
    private readonly List<RecurringTemplate> _templates = [];
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private int _joinSeq;

    private static readonly string[] Palette =
        ["#dfe6cf", "#f0dcc3", "#d9e0ee", "#eed9d9", "#d9eee4"];

    public TestScenario(string householdName = "Testhushåll", string currency = "SEK")
    {
        HouseholdId = Guid.NewGuid();
        _household = new Household
        {
            Id = HouseholdId,
            Name = householdName,
            Currency = currency,
            CreatedAt = _now
        };
    }

    public Guid HouseholdId { get; }

    /// <summary>Member ids in membership (join) order.</summary>
    public IReadOnlyList<Guid> MemberIds => _memberships.Select(m => m.MemberId).ToList();

    private DateOnly Day(int offset) => DateOnly.FromDateTime(_now.UtcDateTime).AddDays(offset);

    /// <summary>Adds a member and joins them to the household. Returns the new member id.
    /// Gets real Identity credentials (a per-member @test.settl.dev email,
    /// <see cref="SeedIds.DevPassword"/>) so <see cref="SettlApiFactory.ClientAs"/> can log
    /// them in.</summary>
    public Guid AddMember(string name, string? avatarColor = null)
    {
        var id = Guid.NewGuid();
        var email = $"{id:N}@test.settl.dev";
        var member = new Member
        {
            Id = id,
            Name = name,
            AvatarColor = avatarColor ?? Palette[_members.Count % Palette.Length],
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };
        member.PasswordHash = new PasswordHasher<Member>().HashPassword(member, SeedIds.DevPassword);
        _members.Add(member);
        _memberships.Add(new HouseholdMembership
        {
            HouseholdId = HouseholdId,
            MemberId = id,
            JoinedAt = _now.AddSeconds(_joinSeq++)
        });
        return id;
    }

    /// <summary>Equal-split expense paid by <paramref name="paidBy"/>, split across all members.</summary>
    public Entry AddEqualExpense(string title, long amountMinor, Guid paidBy, int dateOffset = -1)
    {
        var entry = BuildSplitEntry(EntryType.Expense, title, amountMinor, paidBy,
            SplitMode.Equal, new Dictionary<Guid, decimal>(), dateOffset, null);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>Split expense with an explicit formula (percent or amount per member).</summary>
    public Entry AddExpense(string title, long amountMinor, Guid paidBy, SplitMode mode,
        IReadOnlyDictionary<Guid, decimal> formula, int dateOffset = -1)
    {
        var entry = BuildSplitEntry(EntryType.Expense, title, amountMinor, paidBy, mode,
            new Dictionary<Guid, decimal>(formula), dateOffset, null);
        _entries.Add(entry);
        return entry;
    }

    /// <summary>An active recurring template with the given cadence and next-post date offset.</summary>
    public RecurringTemplate AddRecurring(string title, long amountMinor, Guid paidBy,
        Cadence cadence, int nextPostOffset, SplitMode mode = SplitMode.Equal,
        IReadOnlyDictionary<Guid, decimal>? formula = null)
    {
        var f = formula ?? new Dictionary<Guid, decimal>();
        var template = new RecurringTemplate
        {
            Id = Guid.NewGuid(),
            HouseholdId = HouseholdId,
            Title = title,
            AmountMinor = amountMinor,
            Cadence = cadence,
            NextPostDate = Day(nextPostOffset),
            PaidByMemberId = paidBy,
            SplitMode = mode,
            Active = true,
            CreatedAt = _now
        };
        foreach (var m in MemberIds)
            template.Shares.Add(new RecurringShare
            {
                RecurringTemplateId = template.Id,
                MemberId = m,
                FormulaValue = mode == SplitMode.Equal ? null : f.TryGetValue(m, out var v) ? v : 0m
            });
        _templates.Add(template);
        return template;
    }

    /// <summary>Persists everything built so far. Called by the factory's SeedAsync overload.</summary>
    public async Task SaveAsync(SettlDbContext db, CancellationToken ct = default)
    {
        db.Members.AddRange(_members);
        db.Households.Add(_household);
        db.HouseholdMemberships.AddRange(_memberships);
        db.Entries.AddRange(_entries);
        db.RecurringTemplates.AddRange(_templates);
        await db.SaveChangesAsync(ct);
    }

    private Entry BuildSplitEntry(EntryType type, string title, long amountMinor, Guid paidBy,
        SplitMode mode, Dictionary<Guid, decimal> formula, int dateOffset, Guid? templateId)
    {
        var entry = new Entry
        {
            Id = Guid.NewGuid(),
            HouseholdId = HouseholdId,
            Type = type,
            Title = title,
            AmountMinor = amountMinor,
            Date = Day(dateOffset),
            CreatedAt = _now,
            PaidByMemberId = paidBy,
            SplitMode = mode,
            RecurringTemplateId = templateId
        };
        foreach (var s in ShareFreezer.Freeze(mode, MemberIds, amountMinor, formula))
            entry.Shares.Add(new EntryShare
            {
                EntryId = entry.Id,
                MemberId = s.MemberId,
                ShareMinor = s.ShareMinor,
                FormulaValue = s.FormulaValue
            });
        return entry;
    }
}
