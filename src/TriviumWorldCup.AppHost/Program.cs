var builder = DistributedApplication.CreateBuilder(args);

// ── Parameters (sourced from appsettings.Development.json > Parameters:*) ────
var footballApiKey  = builder.AddParameter("football-api-key",  secret: true);
var vapidPublicKey  = builder.AddParameter("vapid-public-key");
var vapidPrivateKey = builder.AddParameter("vapid-private-key", secret: true);
var vapidSubject    = builder.AddParameter("vapid-subject");

// ── PostgreSQL ────────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("pg")
    .WithDataVolume("twc-postgres-data")
    .WithPgAdmin();

var db = postgres.AddDatabase("Postgres");

// ── .NET API ──────────────────────────────────────────────────────────────────
var api = builder.AddProject<Projects.TriviumWorldCup_Api>("api")
    .WithReference(db)
    .WaitFor(postgres)
    .WithEnvironment("Football__ApiKey",        footballApiKey)
    .WithEnvironment("Push__VapidPublicKey",    vapidPublicKey)
    .WithEnvironment("Push__VapidPrivateKey",   vapidPrivateKey)
    .WithEnvironment("Push__VapidSubject",      vapidSubject);

// ── React / Vite dev server ───────────────────────────────────────────────────
builder.AddNpmApp("web", "../TriviumWorldCup.Web", "dev")
    .WithReference(api)
    .WaitFor(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
