using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TriviumWorldCup.Api.Admin;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Link;
using TriviumWorldCup.Api.Data;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.E2E;
using TriviumWorldCup.Api.Ingestion;
using TriviumWorldCup.Api.Knockout;
using TriviumWorldCup.Api.Leaderboard;
using TriviumWorldCup.Api.Predictions;
using TriviumWorldCup.Api.Profiles;
using TriviumWorldCup.Api.Push;
using TriviumWorldCup.Api.Scoring;
using TriviumWorldCup.Api.Standings;
using TriviumWorldCup.Api.Tournament;

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
    // GroupPrediction — Id is "{UserId}_{FixtureId}" composite key.
    opts.Schema.For<GroupPrediction>().Identity(p => p.Id);
    // KnockoutPrediction — Id is "{UserId}_{SlotKey}" composite key.
    opts.Schema.For<KnockoutPrediction>().Identity(p => p.Id);
    // TournamentPrediction — Id equals the auth UserId (one per member).
    opts.Schema.For<TournamentPrediction>().Identity(p => p.Id);
    // GoalEvent — Id is Guid (Marten picks this up by convention, but we register explicitly).
    opts.Schema.For<GoalEvent>().Identity(e => e.Id);
    // MemberScore — Id equals UserId (one document per member).
    opts.Schema.For<MemberScore>().Identity(s => s.Id);
    // ResultOverride — audit log for manual admin overrides (TWC-16).
    opts.Schema.For<ResultOverride>().Identity(o => o.Id);
    // PushSubscription — Web Push device subscription (TWC-18).
    opts.Schema.For<PushSubscription>().Identity(p => p.Id);
    // InviteUser — admin-managed users for the link auth provider.
    opts.Schema.For<InviteUser>().Identity(u => u.Id).Index(u => u.Email!);
}).UseLightweightSessions()
  .ApplyAllDatabaseChangesOnStartup(); // Warm up all collection schemas on startup instead of lazily per-request

// Scoring recompute service — TWC-8
builder.Services.AddScoped<ScoringRecomputeService>();

// Knockout bracket resolver — TWC-32
builder.Services.AddScoped<KnockoutBracketResolver>();

// Ingestion status store — singleton, updated by ResultIngestionJob each poll cycle (TWC-16)
builder.Services.AddSingleton<IngestionStatusStore>();

// Result ingestion pipeline — TWC-9 (Quartz + FootballApiClient)
// ScoringRecomputeService guard inside AddIngestion prevents double-registration.
builder.Services.AddIngestion(builder.Configuration);

// Push notification services — TWC-18 (WebPushClient + PushReminderJob)
// If VAPID keys are absent, a warning is logged and the job is not registered.
builder.Services.AddPushServices(builder.Configuration);

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

// Auth endpoints — link provider (includes /auth/me and /auth/link/login)
// TWC-20: swap for EntraAuthEndpoints when Entra is wired up.
var activeProvider = (app.Configuration["Auth:Provider"] ?? "link").ToLowerInvariant();
if (activeProvider != "entra")
    app.MapLinkAuthEndpoints();

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

// Fixture + team read endpoints — GET /fixtures, GET /teams
app.MapFixtureEndpoints();

// Group prediction endpoints — GET/POST/PUT /predictions/group/{fixtureId}
app.MapGroupPredictionEndpoints();

// Player roster endpoint — GET /players
app.MapPlayerEndpoints();

// Tournament prediction endpoints — GET/POST/PUT /predictions/tournament
app.MapTournamentPredictionEndpoints();

// Standings endpoints — GET /scores/me
app.MapStandingsEndpoints();

// Leaderboard endpoints -- GET /leaderboard, GET /leaderboard/{userId}
app.MapLeaderboardEndpoints();

// Admin endpoints -- GET/POST /admin/ingestion, /admin/fixtures/{id}/result, etc. (TWC-16)
app.MapAdminEndpoints();

// Push subscription endpoints -- GET /push/vapid-public-key, POST/DELETE /push/subscribe (TWC-18)
app.MapPushEndpoints();

// Knockout bracket slot endpoints -- GET /knockout/slots
app.MapKnockoutSlotEndpoints();

// Knockout prediction endpoints -- GET/POST/PUT /predictions/knockout/{slotKey}
app.MapKnockoutPredictionEndpoints();

// E2E test-control endpoints — Development only (TWC-22).
// Provides seed/reset, fixture kickoff override, and deterministic result injection.
// Intentionally excluded from Staging and Production.
if (app.Environment.IsDevelopment())
    app.MapTestControlEndpoints();

// ── Tournament seed ───────────────────────────────────────────────────────────
// Idempotent: exits immediately if data is already present.
var documentStore = app.Services.GetRequiredService<IDocumentStore>();
await TournamentSeed.SeedAsync(documentStore);

app.Run();

// Make Program visible to test projects
public partial class Program { }
