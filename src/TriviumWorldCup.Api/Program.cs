using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Mock;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException(
        "PostgreSQL connection string not configured. " +
        "Set ConnectionStrings__Postgres or POSTGRES_CONNECTION_STRING.");

// ── Services ─────────────────────────────────────────────────────────────────

// Marten document store (no documents defined yet — extended by feature stories)
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "twc";
}).UseLightweightSessions();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionStringFactory: sp => connectionString,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "postgres"]);

// Auth abstraction (mock provider in dev/demo; swap via Auth:Provider config for TWC-20)
builder.Services.AddAuthAbstraction(builder.Configuration, builder.Environment);

// OpenAPI (Swagger) — development convenience only
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Auth middleware — resolves current user for all downstream handlers
app.UseCurrentUser();

// Mock auth endpoints — only registered when mock provider is active
if (app.Configuration["Auth:Provider"]?.ToLowerInvariant() != "entra")
    app.MapMockAuthEndpoints();

// Health endpoint — used by Docker Compose health check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    }
});

// Minimal liveness ping (no DB dependency) — useful during startup
app.MapGet("/ping", () => Results.Ok(new { status = "ok" }))
   .WithName("Ping")
   .WithTags("health");

app.Run();

// Make Program visible to test projects
public partial class Program { }
