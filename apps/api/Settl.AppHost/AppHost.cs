var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Settl_Api>("api");

if (builder.ExecutionContext.IsPublishMode)
{
    // ADR-0009: single Docker container per host, Docker Compose on a self-hosted VPS,
    // generated via `aspire publish`. The Vite dev-server resource below is dev-only
    // (Aspire's JS hosting is never a production web server, per ADR-0008) and must not
    // appear in the publish graph — the SPA is instead built into the API's own image
    // by apps/api/Settl.Api/Dockerfile.
    var dockerEnv = builder.AddDockerComposeEnvironment("settl")
        .WithDashboard(false);

    api.WithComputeEnvironment(dockerEnv)
        .PublishAsDockerFile(container => container
            .WithDockerfile(contextPath: "../../..", dockerfilePath: "apps/api/Settl.Api/Dockerfile", stage: "final")
            .WithExternalHttpEndpoints()
            // SQLite lives on a mounted volume, never baked into the image; migrations
            // run at container startup (Program.cs), not at build/release time.
            .WithVolume("settl-data", "/data")
            .WithEnvironment("ConnectionStrings__Settl", "Data Source=/data/settl.db"))
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
