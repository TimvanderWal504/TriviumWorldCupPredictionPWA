using Marten;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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
using TriviumWorldCup.Api.Verification;

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
    opts.Schema.For<Fixture>().Identity(f => f.Id).Index(f => f.Status);
    opts.Schema.For<KnockoutSlot>().Identity(s => s.Id).Index(s => s.Status);
    // Player.Id is Guid — Marten picks this up by convention.
    opts.Schema.For<Player>().Identity(p => p.Id);
    // UserProfile — Id equals the auth UserId (string).
    opts.Schema.For<UserProfile>().Identity(p => p.Id);
    // GroupPrediction — Id is "{UserId}_{FixtureId}" composite key.
    opts.Schema.For<GroupPrediction>().Identity(p => p.Id).Index(p => p.UserId);
    // KnockoutPrediction — Id is "{UserId}_{SlotKey}" composite key.
    opts.Schema.For<KnockoutPrediction>().Identity(p => p.Id).Index(p => p.UserId);
    // TournamentPrediction — Id equals the auth UserId (one per member).
    opts.Schema.For<TournamentPrediction>().Identity(p => p.Id);
    // GoalEvent — Id is Guid (Marten picks this up by convention, but we register explicitly).
    opts.Schema.For<GoalEvent>().Identity(e => e.Id).Index(e => e.FixtureId);
    // CardEvent — disciplinary cards per fixture.
    opts.Schema.For<CardEvent>().Identity(e => e.Id).Index(e => e.FixtureId);
    // SubstitutionEvent — player substitutions per fixture.
    opts.Schema.For<SubstitutionEvent>().Identity(e => e.Id).Index(e => e.FixtureId);
    // VarEvent — VAR decisions per fixture.
    opts.Schema.For<VarEvent>().Identity(e => e.Id).Index(e => e.FixtureId);
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

// Points verifier — independent re-derivation, read-only (GET /admin/scoring/verify)
builder.Services.AddScoped<ScoreVerifier>();

// Ingestion status store — singleton, updated by ResultIngestionJob each poll cycle (TWC-16)
builder.Services.AddSingleton<IngestionStatusStore>();

// Result ingestion pipeline — TWC-9 (Quartz + FootballApiClient)
// ScoringRecomputeService guard inside AddIngestion prevents double-registration.
builder.Services.AddIngestion(builder.Configuration);

// Push notification services — TWC-18 (WebPushClient + PushReminderJob)
// If VAPID keys are absent, a warning is logged and the job is not registered.
builder.Services.AddPushServices(builder.Configuration);

// Output cache policies — tag-evictable entries get invalidated by the ingestion job / scoring service.
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("leaderboard",    p => p.Expire(TimeSpan.FromSeconds(20)).Tag("leaderboard"));
    options.AddPolicy("fixtures",       p => p.Expire(TimeSpan.FromSeconds(20)).Tag("fixtures"));
    options.AddPolicy("knockout-slots", p => p.Expire(TimeSpan.FromSeconds(20)).Tag("knockout-slots"));
    options.AddPolicy("teams",          p => p.Expire(TimeSpan.FromMinutes(5)));
    options.AddPolicy("players",        p => p.Expire(TimeSpan.FromMinutes(5)));
});

// Rate limiting — TWC-69. Fixed-window per-IP limiter applied to /auth/link/* (signup/login are
// unauthenticated and would otherwise allow unlimited token-guessing / signup-domain probing).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-link", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

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
builder.Services.AddSwaggerGen(options =>
    options.CustomSchemaIds(t => t.FullName?.Replace('+', '.')));

// Forwarded headers — TWC-68. The API sits behind a TLS-terminating ingress (Azure Container
// Apps' internal ingress, itself reached via the external `twc-web` ACA's Cloudflare-fronted
// HTTPS listener / Docker Compose reverse proxy). Without this, HttpContext.Request.IsHttps is
// always false for the origin request, so the link-auth session cookie (Set-Cookie Secure=…,
// see LinkAuthEndpoints.cs) never gets the Secure flag even when the original client request
// was HTTPS.
//
// ACA does not publish a fixed, documented set of ingress proxy IPs (unlike e.g. Azure Front
// Door), so KnownProxies/KnownNetworks can't be pinned reliably. ASP.NET Core's default
// ForwardedHeadersOptions restricts trusted proxies to loopback only, which would silently
// drop the header in ACA. Given the API's internal ingress is only reachable from inside the
// twc-dev Container Apps environment (not exposed to the public Internet directly — see
// PROGRESS.md Azure Migration notes) we explicitly trust all proxies for this header by
// clearing KnownNetworks/KnownProxies, matching the documented pattern for platforms with a
// managed, non-enumerable proxy layer (App Service, ACA, Container Instances behind a
// platform-managed ingress).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseForwardedHeaders();

app.UseSwagger();
app.UseSwaggerUI();

app.UseOutputCache();

app.UseRateLimiter();

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

// Admin stats endpoint -- GET /admin/stats
app.MapStatsEndpoints();

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
