using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Settl.Api.Data;
using Settl.Api.Services;

namespace Settl.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API against an ISOLATED database per factory instance. Uses a SQLite
/// in-memory database with a single connection kept open for the factory lifetime, so the
/// schema/data persist for as long as the factory lives. Two factories never share state.
///
/// Deviations from production wiring, for deterministic tests:
///   • The app's <see cref="SettlDbContext"/> / DbContextOptions registrations are removed and
///     re-added against the test connection.
///   • The <see cref="RecurringPostingService"/> hosted service is removed so no background
///     posting mutates state mid-test. To test recurrence deterministically, either call the
///     pure <c>RecurrenceCalculator</c> / <c>RecurringPoster</c> functions directly, or new up a
///     <see cref="RecurringPostingService"/> and invoke <c>PostDueCycles</c> yourself.
///   • Environment is "Testing" so Program's Development-only migrate+seed block does NOT run;
///     seeding is explicit via <see cref="SeedCanonicalAsync"/> / <see cref="SeedAsync"/>.
/// </summary>
public sealed class SettlApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    public SettlApiFactory()
    {
        // Unique, in-memory, shared-cache DB. Keeping the connection open holds the DB alive;
        // handing this same connection to EF means every scope sees the identical schema/data.
        _connection = new SqliteConnection(
            $"Data Source=file:settl-tests-{Guid.NewGuid():N}?mode=memory&cache=shared");
        _connection.Open();

        // Build the schema once from the current model (provider-portable, no migrations needed).
        var options = new DbContextOptionsBuilder<SettlDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new SettlDbContext(options);
        ctx.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Drop the app's SQLite-file DbContext registration and re-point at the test DB.
            services.RemoveAll<DbContextOptions<SettlDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SettlDbContext>();
            services.AddDbContext<SettlDbContext>(options => options.UseSqlite(_connection));

            // Remove ONLY the recurring-posting background service (leave framework hosted
            // services intact) so tests are deterministic.
            var recurring = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType == typeof(RecurringPostingService))
                .ToList();
            foreach (var d in recurring) services.Remove(d);
        });
    }

    /// <summary>
    /// An <see cref="HttpClient"/> authenticated as a specific member — logs in with that
    /// member's seeded/dev-scenario credentials (<see cref="SeedIds.DevPassword"/> for both
    /// <see cref="DbInitializer"/> and <see cref="TestScenario"/> members) and keeps the auth
    /// cookie for subsequent requests (WebApplicationFactory's default client handles cookies).
    /// </summary>
    public HttpClient ClientAs(Guid memberId)
    {
        var client = CreateClient();
        var email = WithDb(db => db.Members
            .Where(m => m.Id == memberId)
            .Select(m => m.Email!)
            .SingleAsync()).GetAwaiter().GetResult();

        var login = client.PostAsJsonAsync("/auth/login",
            new { Email = email, Password = SeedIds.DevPassword }).GetAwaiter().GetResult();
        login.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>The most recent invite accept link recorded by <see cref="DevEmailSender"/> —
    /// the same in-memory side channel GET /dev/invites/latest reads from. Tests use this
    /// instead of that endpoint since it's Development-only and the test host runs "Testing".</summary>
    public string? LastDevInviteAcceptUrl => Services.GetRequiredService<DevInviteLinkStore>().LastAcceptUrl;

    /// <summary>Runs work against a fresh scoped <see cref="SettlDbContext"/> and returns a result.</summary>
    public async Task<T> WithDb<T>(Func<SettlDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();
        return await work(db);
    }

    /// <summary>Runs work against a fresh scoped <see cref="SettlDbContext"/>.</summary>
    public async Task WithDb(Func<SettlDbContext, Task> work)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();
        await work(db);
    }

    /// <summary>Seeds the canonical fixture (Du/Sam/Priya, Lönnvägen 3, Familjen, …). Idempotent.</summary>
    public Task SeedCanonicalAsync() => WithDb(db => DbInitializer.SeedAsync(db));

    /// <summary>Persists an ad-hoc <see cref="TestScenario"/> into the isolated DB.</summary>
    public Task SeedAsync(TestScenario scenario) => WithDb(db => scenario.SaveAsync(db));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
