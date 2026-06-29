using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tournament;

/// <summary>
/// Read-only knockout bracket endpoints.
/// </summary>
public static class KnockoutSlotEndpoints
{
    public static IEndpointRouteBuilder MapKnockoutSlotEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /knockout/slots — all 32 bracket slots with current status. Public.
        routes.MapGet("/knockout/slots", async (IDocumentSession session, CancellationToken ct) =>
        {
            var slots = await session.Query<KnockoutSlot>()
                .OrderBy(s => s.Round)
                .ThenBy(s => s.SlotNumber)
                .ToListAsync(ct);

            var dtos = slots.Select(s => new KnockoutSlotDto(
                SlotKey:           s.SlotKey,
                Round:             s.Round.ToString(),
                SlotNumber:        s.SlotNumber,
                HomeTeamId:        s.HomeTeamId,
                AwayTeamId:        s.AwayTeamId,
                KickoffUtc:        s.KickoffUtc,
                Venue:             s.Venue,
                City:              s.City,
                Status:            s.Status.ToString(),
                HomeScore:         s.HomeScore,
                AwayScore:         s.AwayScore,
                PenaltyHomeScore:  s.PenaltyHomeScore,
                PenaltyAwayScore:  s.PenaltyAwayScore,
                WinnerTeamId:      s.WinnerTeamId
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetKnockoutSlots")
        .WithTags("knockout")
        .WithSummary("Returns all 32 knockout bracket slots with current team assignments and results.")
        .CacheOutput("knockout-slots");

        // GET /knockout-slots/results — completed knockout slots with the current user's
        // prediction and points (streak-aware, tournament order). Requires auth.
        routes.MapGet("/knockout-slots/results", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var slots = await session.Query<KnockoutSlot>()
                .Where(s => s.Status == MatchStatus.Completed && s.WinnerTeamId != null)
                .OrderBy(s => s.Round)
                .ThenBy(s => s.SlotNumber)
                .ToListAsync(ct);

            if (slots.Count == 0)
                return Results.Ok(Array.Empty<KnockoutSlotResultDto>());

            var teams = await session.Query<Team>().ToListAsync(ct);
            var teamMap = teams.ToDictionary(t => t.Id);

            var predictions = await session.Query<KnockoutPrediction>()
                .Where(p => p.UserId == user.UserId)
                .ToListAsync(ct);
            var predBySlot = predictions.ToDictionary(p => p.SlotKey);

            var streak = 0;
            var dtos = new List<KnockoutSlotResultDto>(slots.Count);

            foreach (var slot in slots)
            {
                MyKnockoutPredictionDto? myPred = null;

                if (predBySlot.TryGetValue(slot.SlotKey, out var pred))
                {
                    var correctWinner = pred.PredictedWinnerTeamId == slot.WinnerTeamId;

                    var scorePoints = pred.PredictedHomeScore.HasValue && pred.PredictedAwayScore.HasValue
                                      && slot.HomeScore.HasValue && slot.AwayScore.HasValue
                        ? GroupMatchScorer.Compute(
                              pred.PredictedHomeScore.Value, pred.PredictedAwayScore.Value,
                              slot.HomeScore.Value, slot.AwayScore.Value)
                        : 0;

                    var advancingPoints = correctWinner ? 5 * (streak + 1) : 0;

                    myPred = new MyKnockoutPredictionDto(
                        pred.PredictedWinnerTeamId,
                        pred.PredictedHomeScore,
                        pred.PredictedAwayScore,
                        scorePoints,
                        advancingPoints,
                        streak + 1);

                    // Streak only updates when the user submitted a prediction.
                    streak = pred.PredictedWinnerTeamId == slot.WinnerTeamId ? streak + 1 : 0;
                }

                teamMap.TryGetValue(slot.HomeTeamId ?? string.Empty, out var homeTeam);
                teamMap.TryGetValue(slot.AwayTeamId ?? string.Empty, out var awayTeam);

                dtos.Add(new KnockoutSlotResultDto(
                    slot.SlotKey,
                    slot.Round.ToString(),
                    slot.SlotNumber,
                    slot.HomeTeamId,
                    homeTeam?.Name,
                    slot.AwayTeamId,
                    awayTeam?.Name,
                    slot.KickoffUtc,
                    slot.Venue,
                    slot.City,
                    slot.Status.ToString(),
                    slot.HomeScore,
                    slot.AwayScore,
                    slot.PenaltyHomeScore,
                    slot.PenaltyAwayScore,
                    slot.WinnerTeamId,
                    myPred));
            }

            return Results.Ok(dtos);
        })
        .WithName("GetKnockoutSlotResults")
        .WithTags("knockout")
        .WithSummary("Returns completed knockout slots with the current user's prediction and points.");

        return routes;
    }
}

/// <summary>Response DTO for a single knockout bracket slot.</summary>
public sealed record KnockoutSlotDto(
    string SlotKey,
    string Round,
    int SlotNumber,
    string? HomeTeamId,
    string? AwayTeamId,
    DateTimeOffset? KickoffUtc,
    string? Venue,
    string? City,
    string Status,
    int? HomeScore,
    int? AwayScore,
    int? PenaltyHomeScore,
    int? PenaltyAwayScore,
    string? WinnerTeamId);

/// <summary>Response DTO for a completed knockout slot including user prediction.</summary>
public sealed record KnockoutSlotResultDto(
    string SlotKey,
    string Round,
    int SlotNumber,
    string? HomeTeamId,
    string? HomeTeamName,
    string? AwayTeamId,
    string? AwayTeamName,
    DateTimeOffset? KickoffUtc,
    string? Venue,
    string? City,
    string Status,
    int? HomeScore,
    int? AwayScore,
    int? PenaltyHomeScore,
    int? PenaltyAwayScore,
    string? WinnerTeamId,
    MyKnockoutPredictionDto? MyPrediction);

/// <summary>The current user's prediction for one knockout slot, with computed points broken down by component.</summary>
public sealed record MyKnockoutPredictionDto(
    string PredictedWinnerTeamId,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    /// <summary>Group-style points for the 90-minute score (0–10).</summary>
    int ScorePoints,
    /// <summary>Advancing-team bonus: 5 × streak multiplier when correct, 0 when wrong.</summary>
    int AdvancingPoints,
    /// <summary>Streak multiplier applied to the advancing-team bonus (1 = no streak, 2 = one previous correct, …).</summary>
    int StreakMultiplier);
