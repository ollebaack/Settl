namespace Settl.Api.Domain;

/// <summary>
/// THE single definition of household membership order: by <see cref="HouseholdMembership.JoinedAt"/>,
/// then <see cref="HouseholdMembership.MemberId"/>. Used everywhere remainder distribution must be
/// deterministic (splitting) so results are consistent across calls.
/// </summary>
public static class MembershipOrder
{
    public static IReadOnlyList<Guid> Order(IEnumerable<HouseholdMembership> memberships) =>
        memberships
            .OrderBy(m => m.JoinedAt)
            .ThenBy(m => m.MemberId)
            .Select(m => m.MemberId)
            .ToList();
}
