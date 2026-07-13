using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Settl.Api.Data;
using Settl.Api.Features;
using Settl.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// SQLite now, provider-portable model → Postgres later is a provider + connection string swap.
// A local SQLite Data Source path is not a secret, so a default in config is fine.
var connectionString = builder.Configuration.GetConnectionString("Settl") ?? "Data Source=settl.db";
builder.Services.AddDbContext<SettlDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHostedService<RecurringPostingService>();

const string WebCorsPolicy = "web";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Migrations run at container startup (ADR-0009) — not at build/release time, since a
// release-phase step wouldn't have access to the mounted SQLite volume. Seeding stays
// dev-only. The "Testing" environment (WebApplicationFactory-based integration tests,
// see SettlApiFactory) builds its schema via EnsureCreated against an isolated in-memory
// DB instead, so it's excluded here to avoid re-applying migrations onto that schema.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();
    db.Database.Migrate();
    if (app.Environment.IsDevelopment())
    {
        await DbInitializer.SeedAsync(db);
    }
}

app.UseHttpsRedirection();
app.UseCors(WebCorsPolicy);

// Serves the built web SPA (apps/web's Vite build output, copied to wwwroot in the
// Dockerfile's runtime stage) alongside the API. No-op in local dev, where wwwroot
// doesn't exist and the Vite dev server (:5173) serves the SPA instead (ADR-0008).
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth");

app.MapMetaEndpoints();
app.MapHouseholdsEndpoints();
app.MapEntriesEndpoints();
app.MapRecurringEndpoints();
app.MapSettlementsEndpoints();
app.MapNudgesEndpoints();

// SPA client-side routing fallback — must come after all API route mappings above.
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
