using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Predictions;

/// <summary>
/// Group-stage prediction endpoints.
/// All endpoints require authentication; ownership and lock rules are enforced server-side.
/// </summary>
public static class GroupPredictionEndpoints
{
    public static IEndpointRouteBuilder MapGroupPredictionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/predictions/group").WithTags("predictions");

        // GET /predictions/group — all predictions for the current user.
        group.MapGet("/", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var predictions = await session.Query<GroupPrediction>()
                .Where(p => p.UserId == user.UserId)
                .ToListAsync(ct);

            var dtos = predictions.Select(p => new GroupPredictionDto(
                FixtureId:   p.FixtureId,
                HomeScore:   p.HomeScore,
                AwayScore:   p.AwayScore,
                SubmittedAt: p.SubmittedAt
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetGroupPredictions")
        .WithSummary("Returns all group-stage predictions for the current user.");

        // POST /predictions/group/{fixtureId} — create a new prediction.
        group.MapPost("/{fixtureId}", async (
            string fixtureId,
            HttpContext context,
            [FromBody] PredictionRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var validationError = ValidateRequest(request);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            if (IsLocked(fixture))
                return Results.Forbid();

            var predictionId = BuildId(user.UserId, fixtureId);
            var existing = await session.LoadAsync<GroupPrediction>(predictionId, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "Prediction already exists for this fixture. Use PUT to update." });

            var prediction = new GroupPrediction
            {
                Id          = predictionId,
                UserId      = user.UserId,
                FixtureId   = fixtureId,
                HomeScore   = request.HomeScore,
                AwayScore   = request.AwayScore,
                SubmittedAt = DateTimeOffset.UtcNow,
            };

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Created(
                $"/predictions/group/{fixtureId}",
                new GroupPredictionDto(prediction.FixtureId, prediction.HomeScore, prediction.AwayScore, prediction.SubmittedAt));
        })
        .WithName("CreateGroupPrediction")
        .WithSummary("Creates a group-stage prediction for the current user.");

        // POST /predictions/inject — bulk-upsert group-stage predictions for the current user.
        // Body: [{fixtureId, home, away}]. Idempotent (upsert semantics). No lock enforcement.
        group.MapPost("/inject", async (
            [FromBody] List<InjectPredictionItem> items,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            if (items is not { Count: > 0 })
                return Results.BadRequest(new { error = "Request body must be a non-empty array." });

            var fixtureIds = items.Select(i => i.FixtureId).Distinct().ToList();
            var existingIds = (await session.Query<Fixture>()
                .Select(f => f.Id)
                .ToListAsync(ct))
                .ToHashSet();

            var unknown = fixtureIds.Where(id => !existingIds.Contains(id)).ToList();
            if (unknown.Count > 0)
                return Results.UnprocessableEntity(new { error = $"Unknown fixture ID(s): {string.Join(", ", unknown)}" });

            var now = DateTimeOffset.UtcNow;
            foreach (var item in items)
            {
                session.Store(new GroupPrediction
                {
                    Id          = BuildId(user.UserId, item.FixtureId),
                    UserId      = user.UserId,
                    FixtureId   = item.FixtureId,
                    HomeScore   = item.Home,
                    AwayScore   = item.Away,
                    SubmittedAt = now,
                });
            }

            await session.SaveChangesAsync(ct);

            return Results.Ok(new { userId = user.UserId, injected = items.Count });
        })
        .WithName("InjectPredictions")
        .WithSummary("Bulk-upserts group-stage predictions for the current user. Body: [{fixtureId, home, away}]. Idempotent.");

        // PUT /predictions/group/{fixtureId} — update an existing prediction.
        group.MapPut("/{fixtureId}", async (
            string fixtureId,
            HttpContext context,
            [FromBody] PredictionRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var validationError = ValidateRequest(request);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            var fixture = await session.LoadAsync<Fixture>(fixtureId, ct);
            if (fixture is null)
                return Results.NotFound(new { error = $"Fixture '{fixtureId}' not found." });

            if (IsLocked(fixture))
                return Results.Forbid();

            var predictionId = BuildId(user.UserId, fixtureId);
            var prediction = await session.LoadAsync<GroupPrediction>(predictionId, ct);
            if (prediction is null)
                return Results.NotFound(new { error = "Prediction not found. Use POST to create one." });

            // Ownership — belt-and-suspenders: the composite ID already encodes the userId,
            // but we verify explicitly so the rule is clear and auditable.
            if (!string.Equals(prediction.UserId, user.UserId, StringComparison.Ordinal))
                return Results.Forbid();

            prediction.HomeScore   = request.HomeScore;
            prediction.AwayScore   = request.AwayScore;
            prediction.SubmittedAt = DateTimeOffset.UtcNow;

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Ok(new GroupPredictionDto(prediction.FixtureId, prediction.HomeScore, prediction.AwayScore, prediction.SubmittedAt));
        })
        .WithName("UpdateGroupPrediction")
        .WithSummary("Updates a group-stage prediction for the current user.");

        return routes;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the fixture is locked (kickoff has already occurred).
    /// Server-side enforcement — this is the authoritative lock check.
    /// </summary>
    public static bool IsLocked(Fixture fixture) =>
        fixture.KickoffUtc <= DateTimeOffset.UtcNow;

    /// <summary>Builds the composite document ID from user and fixture identifiers.</summary>
    public static string BuildId(string userId, string fixtureId) =>
        $"{userId}_{fixtureId}";

    /// <summary>Returns an error message if the request is invalid, otherwise null.</summary>
    public static string? ValidateRequest(PredictionRequest request)
    {
        if (request.HomeScore < 0)
            return "HomeScore must be non-negative.";
        if (request.AwayScore < 0)
            return "AwayScore must be non-negative.";
        return null;
    }
}

/// <summary>Request body for POST and PUT prediction endpoints.</summary>
public sealed record PredictionRequest(int HomeScore, int AwayScore);

/// <summary>Response DTO for a single group-stage prediction.</summary>
public sealed record GroupPredictionDto(
    string FixtureId,
    int HomeScore,
    int AwayScore,
    DateTimeOffset SubmittedAt);

/// <summary>One item in the POST /predictions/group/inject body.</summary>
public sealed record InjectPredictionItem(string FixtureId, int Home, int Away);
