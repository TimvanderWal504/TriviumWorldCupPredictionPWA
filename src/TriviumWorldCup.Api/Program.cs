using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Mock;
using TriviumWorldCup.Api.Data;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Profiles;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException(
        "PostgreSQL connection string not configured. " +
        "Set ConnectionStrings__Postgres or POSTGRES_CONNECTION_STRING.");

// ── Services ─────────────────────────────────────────────────────────────────

// Marten document store — tournament document types registered here.
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "twc";

    // Tournament documents — all use string identities (natural keys).
    opts.Schema.For<Team>().Identity(t => t.Id);
    opts.Schema.For<Group>().Identity(g => g.Id);
    opts.Schema.For<Fixture>().Identity(f => f.Id);
    opts.Schema.For<KnockoutSlot>().Identity(s => s.Id);
    // Player.Id is Guid — Marten picks this up by convention.
    opts.Schema.For<Player>().Identity(p => p.Id);
    // UserProfile — Id equals the auth UserId (string).
    opts.Schema.For<UserProfile>().Identity(p => p.Id);
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

// Profile endpoints — GET / POST / PUT /profile
app.MapProfileEndpoints();

// ── Tournament seed ───────────────────────────────────────────────────────────
// Idempotent: exits immediately if data is already present.
var documentStore = app.Services.GetRequiredService<IDocumentStore>();
await TournamentSeed.SeedAsync(documentStore);

app.Run();

// Make Program visible to test projects
public partial class Program { }
