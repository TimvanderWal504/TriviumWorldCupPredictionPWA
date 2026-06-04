using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.E2E;

/// <summary>
/// Test-control endpoints for the E2E suite (TWC-22).
/// ONLY registered when ASPNETCORE_ENVIRONMENT is not Production.
///
/// These endpoints expose:
///   POST /e2e/reset          — wipe all user-generated data (predictions, profiles, scores)
///   POST /e2e/seed/profile   — create a profile for a named seeded user
///   POST /e2e/fixtures/{id}/kickoff — override a fixture's KickoffUtc (move to past/future)
///   POST /e2e/fixtures/{id}/result  — set result + status without triggering the live API
///
/// Football API and live feed are irrelevant: the ingestion job only runs if FOOTBALL__APIKEY
/// is set, which it is not in the test environment.
/// </summary>
public static class TestControlEndpoints
{
    public static IEndpointRouteBuilder MapTestControlEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/e2e")
            .WithTags("e2e-test-control");

        // ── POST /e2e/reset ───────────────────────────────────────────────────
        // Deletes all user-authored data so each test spec starts clean.
        // Tournament reference data (teams, fixtures, groups, knockout slots, players) is kept.
        group.MapPost("/reset", async (IDocumentSession session, CancellationToken ct) =>
        {
            // Delete all mutable user data by loading then deleting individually.
            // Marten's DeleteWhere requires a LINQ predicate and operates per-batch;
            // this pattern is explicit and avoids version-specific API surface.
            foreach (var doc in await session.Query<UserProfile>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<GroupPrediction>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<KnockoutPrediction>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<TournamentPrediction>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<MemberScore>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<GoalEvent>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<ResultOverride>().ToListAsync(ct))
                session.Delete(doc);
            foreach (var doc in await session.Query<PushSubscription>().ToListAsync(ct))
                session.Delete(doc);

            // Reset all fixture statuses and scores to initial seeded values
            var fixtures = await session.Query<Fixture>().ToListAsync(ct);
            foreach (var f in fixtures)
            {
                f.Status    = MatchStatus.Scheduled;
                f.HomeScore = null;
                f.AwayScore = null;
                session.Store(f);
            }

            await session.SaveChangesAsync(ct);
            return Results.Ok(new { reset = true });
        })
        .WithName("E2eReset")
        .WithSummary("Wipes all user-authored data; resets fixture statuses. Non-Production only.");

        // ── POST /e2e/reset/fixtures-kickoff ──────────────────────────────────
        // Restores all fixtures to their canonical seeded kickoff times.
        group.MapPost("/reset/fixtures-kickoff", async (IDocumentSession session, CancellationToken ct) =>
        {
            // Reload canonical kickoffs from the in-memory seed source
            var canonicalKickoffs = Data.SeedData.FixturesData.All
                .ToDictionary(f => f.Id, f => f.KickoffUtc);

            var fixtures = await session.Query<Fixture>().ToListAsync(ct);
            foreach (var f in fixtures)
            {
                if (canonicalKickoffs.TryGetValue(f.Id, out var kickoff))
                {
                    f.KickoffUtc = kickoff;
                    session.Store(f);
                }
            }

            await session.SaveChangesAsync(ct);
            return Results.Ok(new { restored = fixtures.Count });
        })
        .WithName("E2eResetFixturesKickoff")
        .WithSummary("Restores all fixture kickoff times to canonical seeded values.");

        // ── POST /e2e/seed/profile ────────────────────────────────────────────
        // Creates or replaces a UserProfile for a named seeded user.
        group.MapPost("/seed/profile", async (
            [FromBody] SeedProfileRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.BadRequest(new { error = "UserId is required." });
            if (string.IsNullOrWhiteSpace(request.DisplayName))
                return Results.BadRequest(new { error = "DisplayName is required." });

            var profile = new UserProfile
            {
                Id          = request.UserId,
                DisplayName = request.DisplayName,
                CountryCode = request.CountryCode ?? "NL",
            };
            session.Store(profile);
            await session.SaveChangesAsync(ct);

            return Results.Ok(new { profile.Id, profile.DisplayName, profile.CountryCode });
        })
        .WithName("E2eSeedProfile")
        .WithSummary("Creates or replaces a UserProfile for a seeded user. Non-Production only.");

        // ── POST /e2e/fixtures/{id}/kickoff ───────────────────────────────────
        // Overrides the KickoffUtc of a fixture to move it to the past or future.
        group.MapPost("/fixtures/{fixtureId}/kickoff", async (
            string fixtureId,
            [FromBody] SetKickoffRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            fixture.KickoffUtc = request.KickoffUtc;
            session.Store(fixture);
            await session.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                fixture.Id,
                fixture.KickoffUtc,
                Status = fixture.Status.ToString(),
            });
        })
        .WithName("E2eSetFixtureKickoff")
        .WithSummary("Overrides the KickoffUtc of a fixture. Use to simulate past/future kickoffs.");

        // ── POST /e2e/fixtures/{id}/result ────────────────────────────────────
        // Sets the result and status of a fixture deterministically without hitting the live API.
        // Triggers scoring recompute so leaderboard/standings reflect the result immediately.
        group.MapPost("/fixtures/{fixtureId}/result", async (
            string fixtureId,
            [FromBody] SetFixtureResultRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            if (request.HomeScore < 0 || request.AwayScore < 0)
                return Results.BadRequest(new { error = "Scores must be non-negative." });

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            fixture.HomeScore = request.HomeScore;
            fixture.AwayScore = request.AwayScore;
            fixture.Status    = MatchStatus.Completed;
            session.Store(fixture);
            await session.SaveChangesAsync(ct);

            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixture.Id,
                fixture.HomeTeamId,
                fixture.AwayTeamId,
                fixture.HomeScore,
                fixture.AwayScore,
                Status = fixture.Status.ToString(),
            });
        })
        .WithName("E2eSetFixtureResult")
        .WithSummary("Injects a fixture result deterministically and triggers scoring recompute.");

        return routes;
    }
}

/// <summary>Request body for POST /e2e/seed/profile.</summary>
public sealed record SeedProfileRequest(string UserId, string DisplayName, string? CountryCode);

/// <summary>Request body for POST /e2e/fixtures/{id}/kickoff.</summary>
public sealed record SetKickoffRequest(DateTimeOffset KickoffUtc);

/// <summary>Request body for POST /e2e/fixtures/{id}/result (test control variant).</summary>
public sealed record SetFixtureResultRequest(int HomeScore, int AwayScore);
