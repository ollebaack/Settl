using Microsoft.EntityFrameworkCore;
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

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SettlDbContext>();
    db.Database.Migrate();
    await DbInitializer.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseCors(WebCorsPolicy);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("GetHealth");

app.MapMetaEndpoints();
app.MapHouseholdsEndpoints();
app.MapEntriesEndpoints();
app.MapRecurringEndpoints();
app.MapSettlementsEndpoints();
app.MapNudgesEndpoints();

app.Run();

public partial class Program;
