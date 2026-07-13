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

var api = builder.AddProject<Projects.Settl_Api>("api")
    .WithReference(settlDb)
    .WaitFor(postgres);

if (!string.IsNullOrWhiteSpace(resendApiKey))
{
    api.WithEnvironment("Resend__ApiKey", resendApiKey);
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
    // Local dev: Postgres runs as an Aspire-managed container with a data volume so the
    // schema/seed survive AppHost restarts (ADR-0010).
    postgres.WithDataVolume();

    builder.AddViteApp("web", "../../web")
        .WithPnpm()
        .WithReference(api)
        .WaitFor(api)
        .WithEndpoint("http", endpoint =>
        {
            endpoint.Port = 5173;
            endpoint.IsProxied = false;
        });
}

builder.Build().Run();
