using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Leaderboard;

/// <summary>
/// Leaderboard API endpoints — TWC-11.
///   GET /leaderboard         — public; ranked list of all members.
///   GET /leaderboard/{userId} — auth required; member drill-down with privacy filter.
/// </summary>
public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/leaderboard").WithTags("leaderboard");

        // ── GET /leaderboard ─────────────────────────────────────────────────
        // Public — no auth required.
        // Returns all members ranked by the tiebreaker chain.
        // Members with no MemberScore appear at the bottom with 0 points.
        group.MapGet("/", async (IDocumentSession session, CancellationToken ct) =>
        {
            var allScores = await session
                .Query<MemberScore>()
                .ToListAsync(ct);

            var allProfiles = await session
                .Query<UserProfile>()
                .ToListAsync(ct);

            var profileById = allProfiles.ToDictionary(p => p.Id);

            // Members with a MemberScore — rank these.
            var ranked = LeaderboardRanker.Rank(allScores);

            // Members with a profile but no MemberScore (no predictions scored yet).
            var scoredUserIds = allScores.Select(s => s.UserId).ToHashSet();
            var unscoredProfiles = allProfiles
                .Where(p => !scoredUserIds.Contains(p.Id))
                .ToList();

            var result = new List<LeaderboardEntryDto>(ranked.Count + unscoredProfiles.Count);

            foreach (var rs in ranked)
            {
                profileById.TryGetValue(rs.Score.UserId, out var profile);
                result.Add(new LeaderboardEntryDto(
                    Rank:             rs.Rank,
                    UserId:           rs.Score.UserId,
                    DisplayName:      profile?.DisplayName ?? rs.Score.UserId,
                    CountryCode:      profile?.CountryCode,
                    TotalPoints:      rs.Score.TotalPoints,
                    GroupMatchPoints: rs.Score.GroupMatchPoints,
                    ChampionPoints:   rs.Score.ChampionPoints,
                    GoldenSixPoints:  rs.Score.GoldenSixPoints));
            }

            // Append unscored members at the bottom.
            // Their rank is (number of ranked members + 1).
            var bottomRank = ranked.Count + 1;
            foreach (var profile in unscoredProfiles)
            {
                result.Add(new LeaderboardEntryDto(
                    Rank:             bottomRank,
                    UserId:           profile.Id,
                    DisplayName:      profile.DisplayName,
                    CountryCode:      profile.CountryCode,
                    TotalPoints:      0,
                    GroupMatchPoints: 0,
                    ChampionPoints:   0,
                    GoldenSixPoints:  0));
            }

            return Results.Ok(result);
        })
        .WithName("GetLeaderboard")
        .WithSummary("Returns all members ranked by total points and tiebreaker chain.");

        // ── GET /leaderboard/{userId} ─────────────────────────────────────────
        // Auth required — viewer identity enforces the privacy rule server-side.
        group.MapGet("/{userId}", async (
            string userId,
            HttpContext context,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var viewer = context.GetAppUser();
            if (!viewer.IsAuthenticated)
                return Results.Unauthorized();

            var targetUserId = userId;

            // Load the target's profile for display name.
            var targetProfile = await session.LoadAsync<UserProfile>(targetUserId, ct);
            if (targetProfile is null)
                return Results.NotFound(new { error = $"Member '{targetUserId}' not found." });

            // Load scoring summary (may be null if never scored).
            var memberScore = await session.LoadAsync<MemberScore>(targetUserId, ct);

            // Load tournament prediction.
            var tournamentPrediction = await session.LoadAsync<TournamentPrediction>(targetUserId, ct);

            // Load all group predictions for the target user.
            var allPredictions = await session
                .Query<GroupPrediction>()
                .Where(p => p.UserId == targetUserId)
                .ToListAsync(ct);

            // Privacy filter — if the viewer is NOT the target, only show locked fixtures.
            var now = DateTimeOffset.UtcNow;
            bool isSelf = string.Equals(viewer.UserId, targetUserId, StringComparison.Ordinal);

            IEnumerable<GroupPrediction> visiblePredictions = isSelf
                ? allPredictions
                : allPredictions; // will be filtered after loading fixture kickoffs below

            // Load fixture documents for all predicted fixture IDs.
            var predictedFixtureIds = allPredictions.Select(p => p.FixtureId).Distinct().ToList();

            IReadOnlyList<Fixture> fixtures;
            if (predictedFixtureIds.Count > 0)
            {
                fixtures = await session
                    .Query<Fixture>()
                    .Where(f => f.Id.IsOneOf(predictedFixtureIds))
                    .ToListAsync(ct);
            }
            else
            {
                fixtures = [];
            }

            var fixtureById = fixtures.ToDictionary(f => f.Id);

            // Apply privacy filter now that we have fixture kickoffs.
            // A fixture is "revealed" once its kickoff time has passed OR it already has a result.
            if (!isSelf)
            {
                visiblePredictions = allPredictions
                    .Where(p => fixtureById.TryGetValue(p.FixtureId, out var f)
                                && (f.KickoffUtc <= now || f.Status == MatchStatus.Completed));
            }

            // Build group prediction DTOs.
            var groupPredictionDtos = new List<GroupPredictionDetailDto>();
            foreach (var pred in visiblePredictions)
            {
                fixtureById.TryGetValue(pred.FixtureId, out var fixture);
                groupPredictionDtos.Add(new GroupPredictionDetailDto(
                    FixtureId:     pred.FixtureId,
                    HomeTeamId:    fixture?.HomeTeamId ?? string.Empty,
                    AwayTeamId:    fixture?.AwayTeamId ?? string.Empty,
                    PredictedHome: pred.HomeScore,
                    PredictedAway: pred.AwayScore,
                    ActualHome:    fixture?.HomeScore,
                    ActualAway:    fixture?.AwayScore,
                    KickoffUtc:    fixture?.KickoffUtc ?? DateTimeOffset.MinValue,
                    Locked:        fixture is not null && (fixture.KickoffUtc <= now || fixture.Status == MatchStatus.Completed)));
            }

            // ── Golden Six detail ─────────────────────────────────────────────

            var goldenSixDtos = new List<GoldenSixDetailDto>();
            string? championTeamId   = tournamentPrediction?.ChampionTeamId;
            string? championTeamName = null;

            if (tournamentPrediction is not null)
            {
                var playerIds = tournamentPrediction.GoldenSixPlayerIds;

                if (playerIds.Count > 0)
                {
                    var players = await session
                        .Query<Player>()
                        .Where(p => p.Id.IsOneOf(playerIds))
                        .ToListAsync(ct);

                    var playerById = players.ToDictionary(p => p.Id);

                    // Count countable goals per picked player.
                    var goalEvents = await session
                        .Query<GoalEvent>()
                        .Where(g => g.Type != GoalType.Shootout
                                 && g.Type != GoalType.OwnGoal
                                 && g.PlayerId.IsOneOf(playerIds))
                        .ToListAsync(ct);

                    var goalCountByPlayer = goalEvents
                        .GroupBy(g => g.PlayerId)
                        .ToDictionary(grp => grp.Key, grp => grp.Count());

                    foreach (var playerId in playerIds)
                    {
                        if (!playerById.TryGetValue(playerId, out var player))
                            continue;

                        var goals  = goalCountByPlayer.GetValueOrDefault(playerId);
                        var points = GoldenSixScorer.ComputeForPlayer(player.Position, goals);

                        goldenSixDtos.Add(new GoldenSixDetailDto(
                            PlayerId: playerId,
                            Name:     player.Name,
                            TeamId:   player.TeamId,
                            Position: player.Position.ToString(),
                            Goals:    goals,
                            Points:   points));
                    }
                }

                // Resolve champion team name if a champion team ID is set.
                if (championTeamId is not null)
                {
                    var team = await session.LoadAsync<Team>(championTeamId, ct);
                    championTeamName = team?.Name;
                }
            }

            var response = new MemberDrillDownDto(
                UserId:           targetUserId,
                DisplayName:      targetProfile.DisplayName,
                TotalPoints:      memberScore?.TotalPoints ?? 0,
                GroupPredictions: groupPredictionDtos,
                GoldenSix:        goldenSixDtos,
                ChampionTeamId:   championTeamId,
                ChampionTeamName: championTeamName);

            return Results.Ok(response);
        })
        .WithName("GetMemberDrillDown")
        .WithSummary("Returns a member's predictions and Golden Six picks. Auth required; privacy rules enforced server-side.");

        return routes;
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>A single row in the leaderboard response.</summary>
public sealed record LeaderboardEntryDto(
    int Rank,
    string UserId,
    string DisplayName,
    string? CountryCode,
    int TotalPoints,
    int GroupMatchPoints,
    int ChampionPoints,
    int GoldenSixPoints);

/// <summary>A member's predicted vs actual scoreline for a single fixture.</summary>
public sealed record GroupPredictionDetailDto(
    string FixtureId,
    string HomeTeamId,
    string AwayTeamId,
    int PredictedHome,
    int PredictedAway,
    int? ActualHome,
    int? ActualAway,
    DateTimeOffset KickoffUtc,
    bool Locked);

/// <summary>Per-player detail in the drill-down Golden Six section.</summary>
public sealed record GoldenSixDetailDto(
    Guid PlayerId,
    string Name,
    string TeamId,
    string Position,
    int Goals,
    int Points);

/// <summary>Full drill-down response for a single member.</summary>
public sealed record MemberDrillDownDto(
    string UserId,
    string DisplayName,
    int TotalPoints,
    IReadOnlyList<GroupPredictionDetailDto> GroupPredictions,
    IReadOnlyList<GoldenSixDetailDto> GoldenSix,
    string? ChampionTeamId,
    string? ChampionTeamName);
