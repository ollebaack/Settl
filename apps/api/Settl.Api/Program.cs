using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
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

// Fail fast in production if the public origin used to build email/invite links is unset.
// AuthEndpoints/InvitesEndpoints fall back to http://localhost:5173 when Web:BaseUrl is
// missing (a dev convenience), which in prod ships confirmation/reset/invite emails whose
// links point at the recipient's own machine — a silent footgun, so surface it at startup.
if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(builder.Configuration["Web:BaseUrl"]))
{
    throw new InvalidOperationException(
        "Web:BaseUrl must be set in production (e.g. Web__BaseUrl=https://settlapp.se). "
        + "Without it, email confirmation, password-reset, and invite links fall back to "
        + "http://localhost:5173 and point at the recipient's own machine.");
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
// The daily nudge-digest pass runs inside RecurringPostingService (reminder-delivery spec — one
// worker, not two). Scoped: it holds a DbContext + email sender per pass. The token minter is
// stateless over the Data Protection key ring, so it's a singleton.
builder.Services.AddScoped<NudgeDigestService>();
builder.Services.AddSingleton<NudgeUnsubscribeTokens>();
// Scrubs the raw phone number off SMS invites once they expire (contacts-phone-sms spec / GDPR).
builder.Services.AddHostedService<ExpiredInviteScrubber>();

// ADR-0005: cookie auth via ASP.NET Identity. Relaxed password policy — consumer app,
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

// Without this, the Data Protection key ring lives only in the running container's
// memory: every redeploy recreates the container, rotates the keys, and silently
// invalidates every outstanding auth cookie, email-confirmation token, and password-reset
// token. DataProtection:KeyPath is only set in production (mounted as a persistent
// volume, same pattern as settl-postgres-data) — dev/test processes aren't recycled
// mid-session, so there's nothing to persist there.
var dataProtectionKeyPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("Settl")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
}

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

// ADR-0005: Resend for real send; falls back to a logging sender whenever no key is
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

// the contacts-phone-sms spec defers the SMS vendor (Sinch/Vonage/Twilio), so only the logging dev sender exists;
// a real ISmsSender is registered here the same way ResendEmailSender is once picked.
builder.Services.AddScoped<ISmsSender, DevSmsSender>();

// Rate-limit the invite-send path. contacts-phone-sms spec: SMS costs money and SMS pumping is a fraud vector,
// so throttling ships WITH the channel (unlike near-free email, tech-debt/0006). Partitioned by
// the acting member (falling back to IP) so one abuser can't drain the SMS budget; a fixed window
// keeps it simple and provider-portable. The default rejection status is 503 — override to 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(ContactsEndpoints.InviteRateLimitPolicy, httpContext =>
    {
        var partitionKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromHours(1),
        });
    });
});

const string WebCorsPolicy = "web";
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();

        if (builder.Environment.IsDevelopment())
        {
            // The web dev server runs on a random per-worktree port under `aspire run
            // --isolated` (ADR-0008), and e2e can bind arbitrary ports too. Cookie auth uses
            // AllowCredentials(), which forbids AllowAnyOrigin(), so instead of pinning one
            // port we reflect any loopback origin (localhost/127.0.0.1/::1). Development-only —
            // production keeps a strict single-origin allow-list below.
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback);
        }
        else if (!string.IsNullOrWhiteSpace(builder.Configuration["Web:BaseUrl"]))
        {
            // Production serves the SPA same-origin from the API's own wwwroot (so CORS is
            // effectively unused), but pin to the configured public origin if one is set.
            policy.WithOrigins(builder.Configuration["Web:BaseUrl"]!);
        }
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

// Migrations run at container startup (ADR-0014) — not at build/release time, since a
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
// After auth so the invite rate-limit policy can partition by the acting member (contacts-phone-sms spec).
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth")
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapInvitesEndpoints();
app.MapContactsEndpoints();
app.MapMetaEndpoints();
app.MapHouseholdsEndpoints();
app.MapEntriesEndpoints();
app.MapRecurringEndpoints();
app.MapSettlementsEndpoints();
app.MapNudgesEndpoints();
app.MapNotificationsEndpoints();
app.MapUnsubscribeEndpoints();

// SPA client-side routing fallback — must come after all API route mappings above. Anonymous
// so the login page (served from here) loads before the user is authenticated.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
