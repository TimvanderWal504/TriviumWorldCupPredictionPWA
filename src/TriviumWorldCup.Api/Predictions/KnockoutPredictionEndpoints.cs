using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Predictions;

/// <summary>
/// Knockout bracket prediction endpoints.
/// All write endpoints require authentication; lock and bracket-progression rules are enforced server-side.
/// </summary>
public static class KnockoutPredictionEndpoints
{
    public static IEndpointRouteBuilder MapKnockoutPredictionEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/predictions/knockout").WithTags("predictions");

        // GET /predictions/knockout — all knockout predictions for the current user.
        group.MapGet("/", async (HttpContext context, IDocumentSession session, ITournamentContext tournament, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var predictions = await session.Query<KnockoutPrediction>()
                .Where(p => p.TournamentId == tournament.TournamentId && p.UserId == user.UserId)
                .ToListAsync(ct);

            var dtos = predictions.Select(MapToDto);
            return Results.Ok(dtos);
        })
        .WithName("GetKnockoutPredictions")
        .WithSummary("Returns all knockout-stage predictions for the current user.");

        // POST /predictions/knockout/{slotKey} — create a new knockout prediction.
        group.MapPost("/{slotKey}", async (
            string slotKey,
            HttpContext context,
            [FromBody] KnockoutPredictionRequest request,
            IDocumentSession session,
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var slot = await session.LoadAsync<KnockoutSlot>(slotKey, ct);
            if (slot is null)
                return Results.NotFound(new { error = $"Slot '{slotKey}' not found." });

            // 422: bracket not yet resolved — teams unknown
            if (slot.HomeTeamId is null || slot.AwayTeamId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "This match's participants are not yet determined. Predictions open once the bracket is set."
                });

            // 403: locked at kickoff
            if (IsLocked(slot))
                return Results.Forbid();

            // 409: prediction already exists — caller should PUT
            var predictionId = BuildId(tournament.TournamentId, user.UserId, slotKey);
            var existing = await session.LoadAsync<KnockoutPrediction>(predictionId, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "Prediction already exists for this slot. Use PUT to update." });

            // Bracket-progression enforcement: winner must be one of the two participants
            var validationError = ValidateWinner(request.PredictedWinnerTeamId, slot);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            var prediction = new KnockoutPrediction
            {
                Id                    = predictionId,
                TournamentId          = tournament.TournamentId,
                UserId                = user.UserId,
                SlotKey               = slotKey,
                PredictedWinnerTeamId = request.PredictedWinnerTeamId,
                PredictedHomeScore    = request.PredictedHomeScore,
                PredictedAwayScore    = request.PredictedAwayScore,
                SubmittedAt           = DateTimeOffset.UtcNow,
            };

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Created($"/predictions/knockout/{slotKey}", MapToDto(prediction));
        })
        .WithName("CreateKnockoutPrediction")
        .WithSummary("Creates a knockout-stage prediction for the current user.");

        // PUT /predictions/knockout/{slotKey} — update an existing knockout prediction.
        group.MapPut("/{slotKey}", async (
            string slotKey,
            HttpContext context,
            [FromBody] KnockoutPredictionRequest request,
            IDocumentSession session,
            ITournamentContext tournament,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var slot = await session.LoadAsync<KnockoutSlot>(slotKey, ct);
            if (slot is null)
                return Results.NotFound(new { error = $"Slot '{slotKey}' not found." });

            // 422: bracket not yet resolved — teams unknown
            if (slot.HomeTeamId is null || slot.AwayTeamId is null)
                return Results.UnprocessableEntity(new
                {
                    error = "This match's participants are not yet determined. Predictions open once the bracket is set."
                });

            // 403: locked at kickoff
            if (IsLocked(slot))
                return Results.Forbid();

            var predictionId = BuildId(tournament.TournamentId, user.UserId, slotKey);
            var prediction = await session.LoadAsync<KnockoutPrediction>(predictionId, ct);
            if (prediction is null)
                return Results.NotFound(new { error = "Prediction not found. Use POST to create one." });

            // Ownership — belt-and-suspenders; composite ID encodes userId but we verify explicitly.
            if (!string.Equals(prediction.UserId, user.UserId, StringComparison.Ordinal))
                return Results.Forbid();

            // Bracket-progression enforcement
            var validationError = ValidateWinner(request.PredictedWinnerTeamId, slot);
            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            prediction.PredictedWinnerTeamId = request.PredictedWinnerTeamId;
            prediction.PredictedHomeScore    = request.PredictedHomeScore;
            prediction.PredictedAwayScore    = request.PredictedAwayScore;
            prediction.SubmittedAt           = DateTimeOffset.UtcNow;

            session.Store(prediction);
            await session.SaveChangesAsync(ct);

            return Results.Ok(MapToDto(prediction));
        })
        .WithName("UpdateKnockoutPrediction")
        .WithSummary("Updates a knockout-stage prediction for the current user.");

        return routes;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the slot is locked (kickoff has occurred or no kickoff is set).
    /// Server-side enforcement — this is the authoritative lock check.
    /// A null kickoff is treated as locked to be safe.
    /// </summary>
    public static bool IsLocked(KnockoutSlot slot) =>
        slot.KickoffUtc is null || slot.KickoffUtc <= DateTimeOffset.UtcNow;

    /// <summary>Builds the composite document ID from tournament, user, and slot identifiers (GEN-1).</summary>
    public static string BuildId(string tournamentId, string userId, string slotKey) =>
        $"{tournamentId}_{userId}_{slotKey}";

    /// <summary>
    /// Returns an error message if the predicted winner is not one of the two slot participants,
    /// otherwise null. Requires HomeTeamId and AwayTeamId to be non-null before calling.
    /// </summary>
    public static string? ValidateWinner(string predictedWinnerTeamId, KnockoutSlot slot)
    {
        if (string.IsNullOrWhiteSpace(predictedWinnerTeamId))
            return "PredictedWinnerTeamId is required.";

        if (!string.Equals(predictedWinnerTeamId, slot.HomeTeamId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(predictedWinnerTeamId, slot.AwayTeamId, StringComparison.OrdinalIgnoreCase))
        {
            return $"PredictedWinnerTeamId must be '{slot.HomeTeamId}' or '{slot.AwayTeamId}'.";
        }

        return null;
    }

    private static KnockoutPredictionDto MapToDto(KnockoutPrediction p) =>
        new(p.SlotKey, p.PredictedWinnerTeamId, p.PredictedHomeScore, p.PredictedAwayScore, p.SubmittedAt);
}

/// <summary>Request body for POST and PUT knockout prediction endpoints.</summary>
public sealed record KnockoutPredictionRequest(
    string PredictedWinnerTeamId,
    int? PredictedHomeScore,
    int? PredictedAwayScore);

/// <summary>Response DTO for a single knockout prediction.</summary>
public sealed record KnockoutPredictionDto(
    string SlotKey,
    string PredictedWinnerTeamId,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    DateTimeOffset SubmittedAt);
