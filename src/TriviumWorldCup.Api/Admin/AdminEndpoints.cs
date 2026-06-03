using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Admin;

/// <summary>
/// Admin API endpoints: ingestion health, manual result overrides, override history, recompute.
/// Every endpoint requires the caller to be in the "admin" role — returns 403 otherwise.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/admin").WithTags("admin");

        // ── GET /admin/ingestion ──────────────────────────────────────────────
        group.MapGet("/ingestion", async (
            HttpContext context,
            IngestionStatusStore statusStore,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var now = DateTimeOffset.UtcNow;
            var pendingCount = await session
                .Query<Fixture>()
                .Where(f => f.Status != MatchStatus.Completed && f.KickoffUtc < now)
                .CountAsync(ct);

            return Results.Ok(new
            {
                lastSuccessfulPoll = statusStore.LastSuccessfulPoll,
                lastAttemptedPoll  = statusStore.LastAttemptedPoll,
                lastError          = statusStore.LastError,
                totalPollCount     = statusStore.TotalPollCount,
                errorCount         = statusStore.ErrorCount,
                pendingFixtureCount = pendingCount,
            });
        })
        .WithName("GetIngestionStatus")
        .WithSummary("Returns ingestion health and pending fixture count.");

        // ── POST /admin/fixtures/{fixtureId}/result ───────────────────────────
        group.MapPost("/fixtures/{fixtureId}/result", async (
            string fixtureId,
            HttpContext context,
            [FromBody] SetResultRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.HomeScore < 0 || request.AwayScore < 0)
                return Results.BadRequest(new { error = "Scores must be non-negative." });

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            fixture.HomeScore = request.HomeScore;
            fixture.AwayScore = request.AwayScore;
            fixture.Status    = MatchStatus.Completed;
            session.Store(fixture);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "fixture",
                TargetId         = fixtureId,
                Description      = $"Set result {request.HomeScore}-{request.AwayScore}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new
            {
                fixture.Id,
                fixture.HomeTeamId,
                fixture.AwayTeamId,
                fixture.HomeScore,
                fixture.AwayScore,
                fixture.Status,
            });
        })
        .WithName("SetFixtureResult")
        .WithSummary("Manually sets the result of a group fixture and triggers recompute.");

        // ── POST /admin/fixtures/{fixtureId}/goals ────────────────────────────
        group.MapPost("/fixtures/{fixtureId}/goals", async (
            string fixtureId,
            HttpContext context,
            [FromBody] AddGoalRequest request,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            if (!Enum.TryParse<GoalType>(request.Type, ignoreCase: true, out var goalType))
                return Results.BadRequest(new { error = $"Invalid goal type '{request.Type}'. Valid values: OpenPlay, PenaltyInMatch, Shootout, OwnGoal." });

            // Deterministic ID based on fixtureId + playerId + minute — same as ingestion strategy
            var deterministicKey = $"admin:{fixtureId}:{request.PlayerId}:{request.Minute}";
            var goalId = DeterministicGuid(deterministicKey);

            var goalEvent = new GoalEvent
            {
                Id        = goalId,
                FixtureId = fixtureId,
                PlayerId  = request.PlayerId,
                Type      = goalType,
                Minute    = request.Minute,
            };
            session.Store(goalEvent);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "goalevent",
                TargetId         = goalId.ToString(),
                Description      = $"Added/replaced goal for player {request.PlayerId} at minute {request.Minute} ({goalType}) in fixture {fixtureId}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.Created(
                $"/admin/fixtures/{fixtureId}/goals",
                new
                {
                    goalEvent.Id,
                    goalEvent.FixtureId,
                    goalEvent.PlayerId,
                    goalEvent.Type,
                    goalEvent.Minute,
                });
        })
        .WithName("AddGoalEvent")
        .WithSummary("Adds or replaces a goal event for a fixture and triggers recompute.");

        // ── DELETE /admin/fixtures/{fixtureId}/goals/{goalEventId} ────────────
        group.MapDelete("/fixtures/{fixtureId}/goals/{goalEventId}", async (
            string fixtureId,
            Guid goalEventId,
            HttpContext context,
            IDocumentSession session,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var goalEvent = await session.LoadAsync<GoalEvent>(goalEventId, ct);
            if (goalEvent is null)
                return Results.NotFound(new { error = $"GoalEvent '{goalEventId}' not found." });

            session.Delete<GoalEvent>(goalEventId);

            var overrideRecord = new ResultOverride
            {
                Id               = Guid.NewGuid(),
                AdminUserId      = user.UserId,
                AdminDisplayName = user.DisplayName,
                OverriddenAt     = DateTimeOffset.UtcNow,
                TargetType       = "goalevent",
                TargetId         = goalEventId.ToString(),
                Description      = $"Deleted goal event {goalEventId} from fixture {fixtureId}",
            };
            session.Store(overrideRecord);

            await session.SaveChangesAsync(ct);
            await scoringService.RecomputeAllAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteGoalEvent")
        .WithSummary("Deletes a goal event from a fixture and triggers recompute.");

        // ── GET /admin/overrides ──────────────────────────────────────────────
        group.MapGet("/overrides", async (
            HttpContext context,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await using var session = store.LightweightSession();

            var overrides = await session
                .Query<ResultOverride>()
                .OrderByDescending(o => o.OverriddenAt)
                .Take(50)
                .ToListAsync(ct);

            return Results.Ok(overrides.Select(o => new
            {
                id              = o.Id,
                adminDisplayName = o.AdminDisplayName,
                overriddenAt    = o.OverriddenAt,
                targetType      = o.TargetType,
                targetId        = o.TargetId,
                description     = o.Description,
            }));
        })
        .WithName("GetOverrides")
        .WithSummary("Returns the most recent 50 manual overrides, newest first.");

        // ── POST /admin/recompute ─────────────────────────────────────────────
        group.MapPost("/recompute", async (
            HttpContext context,
            ScoringRecomputeService scoringService,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsInRole("admin"))
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            await scoringService.RecomputeAllAsync(ct);

            return Results.Ok(new { message = "Recompute triggered" });
        })
        .WithName("ForceRecompute")
        .WithSummary("Forces a full scoring recompute for all members.");

        return routes;
    }

    /// <summary>
    /// Simple deterministic Guid based on a string key using MD5 (sufficient for admin override
    /// deduplication — not security-sensitive, just a stable upsert key).
    /// </summary>
    private static Guid DeterministicGuid(string key)
    {
        var hash = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}

/// <summary>Request body for POST /admin/fixtures/{id}/result.</summary>
public sealed record SetResultRequest(int HomeScore, int AwayScore);

/// <summary>Request body for POST /admin/fixtures/{id}/goals.</summary>
public sealed record AddGoalRequest(Guid PlayerId, string Type, int Minute);
