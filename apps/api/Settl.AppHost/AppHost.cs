using System.Net;
using System.Net.Sockets;

var builder = DistributedApplication.CreateBuilder(args);

// ADR-0010: Postgres replaces SQLite in both dev and prod, ahead of ADR-0004's original
// trigger. AddPostgres is container-backed (Aspire.Hosting.PostgreSQL), so Docker
// Desktop/Podman is now a required local-dev dependency — this revises ADR-0008's
// "no Docker needed" consequence.
var postgres = builder.AddPostgres("postgres");
var settlDb = postgres.AddDatabase("Settl");

// ADR-0011: Resend sends invite email. Read via configuration (user-secrets or an
// env var on the AppHost — e.g. `dotnet user-secrets set Resend:ApiKey ... --project
// apps/api/Settl.AppHost` — never appsettings*.json) rather than an Aspire AddParameter:
// a required parameter prompts/blocks `dotnet run` when unset, and this key is meant to
// stay unset in local dev, where the API falls back to its logging-only DevEmailSender.
var resendApiKey = builder.Configuration["Resend:ApiKey"];
// Optional: overrides the API's default from-address ("Settl <no-reply@settl.dev>").
// Needed whenever the sending domain isn't verified in Resend — e.g. Resend's shared
// "onboarding@resend.dev" sender, which only delivers to the account owner's own inbox.
var resendFromAddress = builder.Configuration["Resend:FromAddress"];

var api = builder.AddProject<Projects.Settl_Api>("api")
    .WithReference(settlDb)
    .WaitFor(postgres);

if (!string.IsNullOrWhiteSpace(resendApiKey))
{
    api.WithEnvironment("Resend__ApiKey", resendApiKey);
}
if (!string.IsNullOrWhiteSpace(resendFromAddress))
{
    api.WithEnvironment("Resend__FromAddress", resendFromAddress);
}

if (builder.ExecutionContext.IsPublishMode)
{
    // ADR-0009: Docker Compose on a self-hosted VPS, generated via `aspire publish`.
    // ADR-0010 adds Postgres as a second, self-hosted container in the same stack
    // (no managed Postgres service). The Vite dev-server resource below is dev-only
    // (Aspire's JS hosting is never a production web server, per ADR-0008) and must not
    // appear in the publish graph — the SPA is instead built into the API's own image
    // by apps/api/Settl.Api/Dockerfile.
    // Named distinctly from the "Settl" database resource above — Aspire resource names
    // are case-insensitive, so "settl" here would collide with "Settl".
    var dockerEnv = builder.AddDockerComposeEnvironment("settl-compose")
        .WithDashboard(false);

    // Postgres data lives on a mounted volume, never baked into the image; migrations
    // run at container startup (Program.cs), not at build/release time (ADR-0009).
    postgres.WithComputeEnvironment(dockerEnv)
        .WithDataVolume("settl-postgres-data");

    api.WithComputeEnvironment(dockerEnv)
        .PublishAsDockerFile(container => container
            .WithDockerfile(contextPath: "../../..", dockerfilePath: "apps/api/Settl.Api/Dockerfile", stage: "final")
            .WithExternalHttpEndpoints())
        // Combining WithHttpEndpoint(port, targetPort) with WithExternalHttpEndpoints()
        // produced a duplicate port entry ("8080:8080" and a bare, ephemeral-host "8080")
        // in the generated docker-compose.yaml — verified by actually running
        // `aspire publish` and inspecting the output, not assumed. Pinning the published
        // port directly on the Compose service sidesteps that.
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Ports.Clear();
            service.Ports.Add("8080:8080");
        });
}
else
{
    // Local dev: deliberately NO data volume — every `pnpm dev` starts Postgres empty,
    // so Program.cs's migrate+seed runs fresh each time. Trades "state survives a
    // restart" for two things worth more in dev: never hitting stale-schema seed data
    // (bit us once already, from data seeded before the Identity migration landed) and
    // being able to re-test signup/invite flows with the same email repeatedly instead
    // of hitting "already exists".

    // HMR's WebSocket breaks through Aspire's proxy (ADR-0008, microsoft/aspire#14470 —
    // still open), so the Vite endpoint must stay proxyless. But a proxyless endpoint needs
    // an explicit port — Aspire won't allocate one, and even `--isolated` won't randomize it
    // (verified: DCP throws "needs to specify a port ... since it isn't using a proxy").
    // Pinning a constant would just move the cross-worktree collision, so grab a fresh free
    // ephemeral port per run instead: different every launch, so parallel worktrees never
    // clash (ADR-0025). AddViteApp passes this to Vite via the PORT env var.
    var webListener = new TcpListener(IPAddress.Loopback, 0);
    webListener.Start();
    var webPort = ((IPEndPoint)webListener.LocalEndpoint).Port;
    webListener.Stop();

    // The browser SPA can only read VITE_-prefixed env vars (Aspire's service-discovery
    // vars are server-side only), so the web can't learn the API's port from WithReference
    // alone — it would fall back to api.ts's hardcoded http://localhost:5000 default. Inject
    // the API's *resolved* origin explicitly (ADR-0025): this is what makes a dynamic API
    // port work end-to-end, including under `aspire run --isolated`, where the API port is
    // randomized per worktree so multiple agents' stacks coexist without collisions.
    builder.AddViteApp("web", "../../web")
        .WithPnpm()
        .WithReference(api)
        .WaitFor(api)
        .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
        .WithEndpoint("http", endpoint =>
        {
            endpoint.IsProxied = false;
            endpoint.Port = webPort;
            endpoint.TargetPort = webPort;
        });
}

builder.Build().Run();
