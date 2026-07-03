using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

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
                return Results.Ok(new KnockoutSlotResultsResponse([], [], [], [], []));

            var teams = await session.Query<Team>().ToListAsync(ct);
            var teamMap = teams.ToDictionary(t => t.Id);

            var predictions = await session.Query<KnockoutPrediction>()
                .Where(p => p.UserId == user.UserId)
                .ToListAsync(ct);
            var predBySlot = predictions.ToDictionary(p => p.SlotKey);

            var memberScore = await session.LoadAsync<MemberScore>(user.UserId, ct);
            var breakdownBySlot = memberScore?.KnockoutBreakdown.ToDictionary(b => b.SlotKey)
                                   ?? new Dictionary<string, KnockoutPredictionScore>();

            var dtos = new List<KnockoutSlotResultDto>(slots.Count);

            foreach (var slot in slots)
            {
                MyKnockoutPredictionDto? myPred = null;

                if (predBySlot.TryGetValue(slot.SlotKey, out var pred))
                {
                    breakdownBySlot.TryGetValue(slot.SlotKey, out var b);

                    myPred = new MyKnockoutPredictionDto(
                        pred.PredictedWinnerTeamId,
                        pred.PredictedHomeScore,
                        pred.PredictedAwayScore,
                        ScorePoints:      b?.ScorePoints ?? 0,
                        AdvancingPoints:  b?.AdvancingPoints ?? 0,
                        StreakMultiplier: b?.StreakMultiplier > 0 ? b.StreakMultiplier : 1);
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

            // Fetch all match events for the completed slots.
            // Events are stored with FixtureId = slot.SlotKey (e.g. "R32-1").
            var slotKeys = slots.Select(s => s.SlotKey).ToList();

            var goals = await session.Query<GoalEvent>()
                .Where(g => g.FixtureId.IsOneOf(slotKeys))
                .ToListAsync(ct);
            var cards = await session.Query<CardEvent>()
                .Where(c => c.FixtureId.IsOneOf(slotKeys))
                .ToListAsync(ct);
            var subs = await session.Query<SubstitutionEvent>()
                .Where(s => s.FixtureId.IsOneOf(slotKeys))
                .ToListAsync(ct);
            var vars = await session.Query<VarEvent>()
                .Where(v => v.FixtureId.IsOneOf(slotKeys))
                .ToListAsync(ct);

            var allPlayerIds = goals.Select(g => g.PlayerId)
                .Concat(cards.Select(c => c.PlayerId))
                .Distinct().ToList();
            var playerMap = allPlayerIds.Count > 0
                ? (await session.Query<Player>().Where(p => p.Id.IsOneOf(allPlayerIds)).ToListAsync(ct))
                    .ToDictionary(p => p.Id)
                : new Dictionary<Guid, Player>();

            var goalDtos = goals.Select(g =>
            {
                playerMap.TryGetValue(g.PlayerId, out var player);
                return new GoalEventDto(g.FixtureId, g.PlayerId,
                    player?.Name ?? g.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                    g.Type.ToString(), g.Minute, g.ExtraMinute);
            }).OrderBy(g => g.Minute).ToList();

            var cardDtos = cards.Select(c =>
            {
                playerMap.TryGetValue(c.PlayerId, out var player);
                return new CardEventDto(c.FixtureId, c.PlayerId,
                    player?.Name ?? c.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                    c.Type.ToString(), c.Minute, c.ExtraMinute);
            }).OrderBy(c => c.Minute).ToList();

            var subDtos = subs.Select(s => new SubstitutionEventDto(
                s.FixtureId, s.PlayerInName, s.PlayerOutName, s.TeamId, s.Minute, s.ExtraMinute
            )).OrderBy(s => s.Minute).ToList();

            var varDtos = vars.Select(v => new VarEventDto(
                v.FixtureId, v.PlayerName, v.TeamId, v.Type.ToString(), v.Minute, v.ExtraMinute
            )).OrderBy(v => v.Minute).ToList();

            return Results.Ok(new KnockoutSlotResultsResponse(dtos, goalDtos, cardDtos, subDtos, varDtos));
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

/// <summary>Response for GET /knockout-slots/results.</summary>
public sealed record KnockoutSlotResultsResponse(
    IReadOnlyList<KnockoutSlotResultDto> Slots,
    IReadOnlyList<GoalEventDto> Goals,
    IReadOnlyList<CardEventDto> Cards,
    IReadOnlyList<SubstitutionEventDto> Substitutions,
    IReadOnlyList<VarEventDto> VarEvents);

/// <summary>The current user's prediction for one knockout slot, with computed points broken down by component.</summary>
public sealed record MyKnockoutPredictionDto(
    string PredictedWinnerTeamId,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    /// <summary>Group-style points for the score prediction — judged at 90 minutes, or at the end of extra time for AET/PEN matches (0–10).</summary>
    int ScorePoints,
    /// <summary>Advancing-team bonus: 5 × streak multiplier when correct, 0 when wrong.</summary>
    int AdvancingPoints,
    /// <summary>Streak multiplier applied to the advancing-team bonus (1 = no streak, 2 = one previous correct, …).</summary>
    int StreakMultiplier);
