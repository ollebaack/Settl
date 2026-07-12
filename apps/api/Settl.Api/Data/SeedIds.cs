namespace Settl.Api.Data;

/// <summary>
/// Stable identifiers used by <see cref="DbInitializer"/>'s canonical seed. Exposed publicly so
/// tests can reach seeded members / households / recurring templates without re-declaring GUID
/// literals. Changing these values changes the seed — treat them as a fixed fixture contract.
/// </summary>
public static class SeedIds
{
    // Members
    public static readonly Guid Du = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Sam = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Priya = new("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Mamma = new("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Pappa = new("55555555-5555-5555-5555-555555555555");

    // Households
    public static readonly Guid Lonnvagen = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Familjen = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // Recurring templates
    public static readonly Guid Rent = new("c0000000-0000-0000-0000-000000000001");     // r1 Hyra
    public static readonly Guid Internet = new("c0000000-0000-0000-0000-000000000002"); // r2
    public static readonly Guid Spotify = new("c0000000-0000-0000-0000-000000000003");  // r3
    public static readonly Guid Cleaning = new("c0000000-0000-0000-0000-000000000004"); // r4 Städhjälp
    public static readonly Guid Netflix = new("c0000000-0000-0000-0000-000000000005");  // r5
}
