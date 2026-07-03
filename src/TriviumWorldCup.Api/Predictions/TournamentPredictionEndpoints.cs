using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Predictions;

/// <summary>
/// Tournament-level prediction endpoints: champion team + Golden Six top scorers.
/// All endpoints require authentication; lock and ownership rules are enforced server-side.
/// </summary>
public static class TournamentPredictionEndpoints
{
    public static IEndpointRouteBuilder MapTournamentPredictionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/predictions/tournament").WithTags("predictions");

        // GET /predictions/tournament/lock — returns whether predictions are locked.
        // Public (no auth required) so the frontend can render the lock banner before a user submits.
        group.MapGet("/lock", async (IDocumentSession session, CancellationToken ct) =>
        {
            var firstKickoff = await GetFirstKickoffAsync(session, ct);
            var locked = TournamentPredictionValidator.IsLocked(firstKickoff, DateTimeOffset.UtcNow);
            return Results.Ok(new { locked });
        })
        .WithName("GetTournamentPredictionLock")
        .AllowAnonymous()
        .WithSummary("Returns whether tournament predictions are currently locked.");

        // GET /predictions/tournament — returns the current user's prediction or 404.
        group.MapGet("/", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var prediction = await session.LoadAsync<TournamentPrediction>(user.UserId, ct);
            return prediction is null
                ? Results.NotFound()
                : Results.Ok(ToDto(prediction));
        })
        .WithName("GetTournamentPrediction")
        .WithSummary("Returns the current user's tournament prediction.");

        // POST /predictions/tournament — creates a new prediction.
        group.MapPost("/", async (
            HttpContext context,
            [FromBody] TournamentPredictionRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            // Lock check — server-side, query the earliest kickoff from Marten.
            var firstKickoff = await GetFirstKickoffAsync(session, ct);
            if (TournamentPredictionValidator.IsLocked(firstKickoff, DateTimeOffset.UtcNow))
                return Results.Json(new { error = "Predictions are locked. The tournament has started." }, statusCode: 403);

            // Validation
            var validationError = await TournamentPredictionValidator.ValidateAsync(request, session, ct);
            if (validationError is not null)
                return Results.UnprocessableEntity(new { error = validationError });

            // Conflict — one prediction per member
            var existing = await session.LoadAsync<TournamentPrediction>(user.UserId, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "Tournament prediction already exists. Use PUT to update." });

            var prediction = new TournamentPrediction
            {
                Id                = user.UserId,
                UserId            = user.UserId,
                ChampionTeamId    = request.ChampionTeamId,
                GoldenSixPlayerIds = request.GoldenSixPlayerIds,
                SubmittedAt       = DateTimeOffset.UtcNow,
            };

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Created("/predictions/tournament", ToDto(prediction));
        })
        .WithName("CreateTournamentPrediction")
        .WithSummary("Creates the current user's tournament prediction.");

        // PUT /predictions/tournament — updates an existing prediction.
        group.MapPut("/", async (
            HttpContext context,
            [FromBody] TournamentPredictionRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            // Lock check — server-side
            var firstKickoff = await GetFirstKickoffAsync(session, ct);
            if (TournamentPredictionValidator.IsLocked(firstKickoff, DateTimeOffset.UtcNow))
                return Results.Json(new { error = "Predictions are locked. The tournament has started." }, statusCode: 403);

            // Validation
            var validationError = await TournamentPredictionValidator.ValidateAsync(request, session, ct);
            if (validationError is not null)
                return Results.UnprocessableEntity(new { error = validationError });

            var prediction = await session.LoadAsync<TournamentPrediction>(user.UserId, ct);
            if (prediction is null)
                return Results.NotFound(new { error = "Tournament prediction not found. Use POST to create one." });

            // Ownership — belt-and-suspenders check
            if (!string.Equals(prediction.UserId, user.UserId, StringComparison.Ordinal))
                return Results.Forbid();

            prediction.ChampionTeamId    = request.ChampionTeamId;
            prediction.GoldenSixPlayerIds = request.GoldenSixPlayerIds;
            prediction.SubmittedAt        = DateTimeOffset.UtcNow;

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Ok(ToDto(prediction));
        })
        .WithName("UpdateTournamentPrediction")
        .WithSummary("Updates the current user's tournament prediction.");

        return routes;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Queries Marten for the effective lock time.
    /// Returns DateTimeOffset.MinValue (always locked) if any fixture is already completed —
    /// this ensures admin-overridden results also lock predictions, not just scheduled kickoff time.
    /// Returns DateTimeOffset.MaxValue (always unlocked) when no fixtures exist.
    /// Otherwise returns the earliest scheduled kickoff.
    /// </summary>
    private static async Task<DateTimeOffset> GetFirstKickoffAsync(IDocumentSession session, CancellationToken ct)
    {
        var fixtures = await session.Query<Fixture>().ToListAsync(ct);
        if (fixtures.Count == 0)
            return DateTimeOffset.MaxValue;
        if (fixtures.Any(f => f.Status == MatchStatus.Completed))
            return DateTimeOffset.MinValue;
        return fixtures.Min(f => f.KickoffUtc);
    }

    private static TournamentPredictionDto ToDto(TournamentPrediction p) =>
        new(p.ChampionTeamId, p.GoldenSixPlayerIds, p.SubmittedAt);
}

/// <summary>
/// Pure-logic validator for tournament predictions — no DB dependency, fully unit-testable.
/// </summary>
public static class TournamentPredictionValidator
{
    /// <summary>
    /// Returns true when the prediction window is closed.
    /// The window closes at (and after) the first kickoff.
    /// </summary>
    public static bool IsLocked(DateTimeOffset firstKickoff, DateTimeOffset now) =>
        now >= firstKickoff;

    /// <summary>
    /// Validates the request body asynchronously (team and player lookups via Marten).
    /// Returns an error message if invalid, otherwise null.
    /// </summary>
    public static async Task<string?> ValidateAsync(
        TournamentPredictionRequest request,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Champion must be a non-empty team ID
        if (string.IsNullOrWhiteSpace(request.ChampionTeamId))
            return "ChampionTeamId must not be empty.";

        // Verify team exists in Marten
        var team = await session.LoadAsync<Team>(request.ChampionTeamId, ct);
        if (team is null)
            return $"Team '{request.ChampionTeamId}' not found.";

        // Exactly 6 player IDs required
        if (request.GoldenSixPlayerIds is null || request.GoldenSixPlayerIds.Count != 6)
            return "GoldenSixPlayerIds must contain exactly 6 player IDs.";

        // Player IDs must be distinct — duplicates would let one player's goals count multiple times.
        if (request.GoldenSixPlayerIds.Distinct().Count() != 6)
            return "GoldenSixPlayerIds must contain 6 distinct players.";

        // Verify every player ID exists in Marten
        foreach (var playerId in request.GoldenSixPlayerIds)
        {
            var player = await session.LoadAsync<Player>(playerId, ct);
            if (player is null)
                return $"Player '{playerId}' not found.";
        }

        return null;
    }

    /// <summary>
    /// Pure (no-DB) validation of count rules only — used by unit tests.
    /// Returns an error message if invalid, otherwise null.
    /// </summary>
    public static string? ValidateGoldenSixCount(List<Guid>? playerIds)
    {
        if (playerIds is null || playerIds.Count != 6)
            return "GoldenSixPlayerIds must contain exactly 6 player IDs.";
        if (playerIds.Distinct().Count() != 6)
            return "GoldenSixPlayerIds must contain 6 distinct players.";
        return null;
    }

    /// <summary>
    /// Pure validation of champion team ID — used by unit tests.
    /// </summary>
    public static string? ValidateChampionTeamId(string? championTeamId)
    {
        if (string.IsNullOrWhiteSpace(championTeamId))
            return "ChampionTeamId must not be empty.";
        return null;
    }
}

/// <summary>Request body for POST and PUT tournament prediction endpoints.</summary>
public sealed record TournamentPredictionRequest(
    string? ChampionTeamId,
    List<Guid> GoldenSixPlayerIds);

/// <summary>Response DTO for a tournament prediction.</summary>
public sealed record TournamentPredictionDto(
    string? ChampionTeamId,
    List<Guid> GoldenSixPlayerIds,
    DateTimeOffset SubmittedAt);
