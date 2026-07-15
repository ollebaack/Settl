using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Settl.Api.Data;
using Settl.Api.Domain;
using Settl.Api.Features;
using Settl.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Dev-only: also write logs to a file, since tools without a view into the console
// (Claude Code) can't otherwise see what the API is doing.
if (builder.Environment.IsDevelopment())
{
    var logPath = Path.Combine(builder.Environment.ContentRootPath, ".logs", "api.log");
    builder.Logging.AddProvider(new FileLoggerProvider(logPath));
}

builder.Services.AddOpenApi();

// Postgres (ADR-0010), provider-portable model — the fallback below is a local-only
// default for running outside the AppHost (e.g. `dotnet ef`); it's not a secret.
// Skipped in "Testing": SettlApiFactory registers its own (SQLite, isolated per test run)
// DbContext, and registering both providers' services in one container throws at runtime.
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Settl")
        ?? "Host=localhost;Port=5432;Database=settl;Username=postgres;Password=postgres";
    builder.Services.AddDbContext<SettlDbContext>(options => options.UseNpgsql(connectionString));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHostedService<RecurringPostingService>();

// ADR-0011: cookie auth via ASP.NET Identity. Relaxed password policy — consumer app,
// not enterprise. No lockout/2FA changes from Identity's defaults.
builder.Services.AddIdentityCore<Member>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<SettlDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

// This is a JSON API, not a server-rendered app — an unauthenticated/forbidden request
// should get a plain status code, not the cookie scheme's default redirect-to-login-page.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// Every endpoint requires auth AND a confirmed email unless explicitly AllowAnonymous or
// opted into the lighter "AuthenticatedOnly" policy — new endpoint files need no per-route
// auth wiring (tech-debt/0003's "nothing else should need to change"). Signed-in-but-
// unconfirmed requests fail the extra requirement (not RequireAuthenticatedUser), which the
// cookie scheme's OnRedirectToAccessDenied above turns into 403 — the signal the web app
// uses to route to /verify-email instead of /login.
builder.Services.AddScoped<IAuthorizationHandler, EmailConfirmedHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuthenticatedOnly", p => p.RequireAuthenticatedUser());
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new EmailConfirmedRequirement())
        .Build();
});

// ADR-0011: Resend for real send; falls back to a logging sender whenever no key is
// configured (always true in local dev — see tech-debt/README on dev-only stand-ins).
builder.Services.AddSingleton<DevEmailLinkStore>();
var resendApiKey = builder.Configuration["Resend:ApiKey"];
if (!string.IsNullOrWhiteSpace(resendApiKey))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
    {
        client.BaseAddress = new Uri("https://api.resend.com/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey);
    });
}
else
{
    builder.Services.AddScoped<IEmailSender, DevEmailSender>();
}

const string WebCorsPolicy = "web";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
    // wwwroot (and thus the SPA fallback below) doesn't exist in dev, so the dashboard's
    // link to the api resource's root would otherwise just 404.
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).AllowAnonymous();
}

// Migrations run at container startup (ADR-0009) — not at build/release time, since a
// release-phase step wouldn't have access to the mounted Postgres volume. Seeding stays
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

// Dev is intentionally all-HTTP (the Vite dev server has no HTTPS support either,
// ADR-0008), and the API's https launch profile exposing :7026 alongside :5000 meant a
// plain http:5000 request — including the browser's CORS preflight OPTIONS — got
// redirected before UseCors ever ran, which browsers reject (redirecting a preflight
// is illegal per the CORS spec). Skipping the redirect in Development avoids that.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Serves the built web SPA (apps/web's Vite build output, copied to wwwroot in the
// Dockerfile's runtime stage) alongside the API. No-op in local dev, where wwwroot
// doesn't exist and the Vite dev server (:5173) serves the SPA instead (ADR-0008).
// Must run before auth: MapFallbackToFile's default route excludes paths with a file
// extension (e.g. /assets/foo.js), so those never match an endpoint and would otherwise
// fall through to the global FallbackPolicy and get rejected with 401 before UseStaticFiles
// ever got a chance to serve them.
app.UseStaticFiles();

app.UseCors(WebCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapInvitesEndpoints();
app.MapMetaEndpoints();
app.MapHouseholdsEndpoints();
app.MapEntriesEndpoints();
app.MapRecurringEndpoints();
app.MapSettlementsEndpoints();
app.MapNudgesEndpoints();

// SPA client-side routing fallback — must come after all API route mappings above. Anonymous
// so the login page (served from here) loads before the user is authenticated.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
