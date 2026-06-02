using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Standings;

/// <summary>
/// Standings API endpoints: GET /scores/me.
/// Returns the current user's scoring summary, category breakdown, rank, and Golden Six detail.
/// TWC-10.
/// </summary>
public static class StandingsEndpoints
{
    public static IEndpointRouteBuilder MapStandingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/scores").WithTags("standings");

        // GET /scores/me — returns the calling user's full standings.
        group.MapGet("/me", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            // Load the user's MemberScore (may not exist if no predictions have been scored yet).
            var myScore = await session.LoadAsync<MemberScore>(user.UserId, ct);

            // Load all MemberScore documents to determine rank.
            var allScores = await session.Query<MemberScore>().ToListAsync(ct);

            var myTotalPoints = myScore?.TotalPoints ?? 0;

            // Rank = count of members with strictly MORE total points + 1.
            var membersAhead = allScores.Count(s => s.TotalPoints > myTotalPoints);
            var rank = membersAhead + 1;
            var totalMembers = allScores.Count;

            // Load tournament prediction to get Golden Six picks.
            var tournamentPrediction = await session.LoadAsync<TournamentPrediction>(user.UserId, ct);

            var goldenSixItems = new List<GoldenSixPlayerDto>();

            if (tournamentPrediction is not null && tournamentPrediction.GoldenSixPlayerIds.Count > 0)
            {
                var playerIds = tournamentPrediction.GoldenSixPlayerIds;

                // Load all 6 player documents.
                var players = await session
                    .Query<Player>()
                    .Where(p => p.Id.IsOneOf(playerIds))
                    .ToListAsync(ct);

                var playerById = players.ToDictionary(p => p.Id);

                // Count countable goals per player (exclude Shootout and OwnGoal).
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
                        continue; // player document missing — skip

                    var goals = goalCountByPlayer.GetValueOrDefault(playerId);
                    var points = GoldenSixScorer.ComputeForPlayer(player.Position, goals);

                    goldenSixItems.Add(new GoldenSixPlayerDto(
                        PlayerId: playerId,
                        Name: player.Name,
                        TeamId: player.TeamId,
                        Position: player.Position.ToString(),
                        Goals: goals,
                        Points: points));
                }
            }

            var response = new MyStandingsDto(
                UserId: user.UserId,
                TotalPoints: myTotalPoints,
                GroupMatchPoints: myScore?.GroupMatchPoints ?? 0,
                ChampionPoints: myScore?.ChampionPoints ?? 0,
                GoldenSixPoints: myScore?.GoldenSixPoints ?? 0,
                Rank: rank,
                TotalMembers: totalMembers,
                GoldenSix: goldenSixItems);

            return Results.Ok(response);
        })
        .WithName("GetMyStandings")
        .WithSummary("Returns the current user's standings, category breakdown, rank, and Golden Six detail.");

        return routes;
    }
}

/// <summary>Response body for GET /scores/me.</summary>
public sealed record MyStandingsDto(
    string UserId,
    int TotalPoints,
    int GroupMatchPoints,
    int ChampionPoints,
    int GoldenSixPoints,
    int Rank,
    int TotalMembers,
    IReadOnlyList<GoldenSixPlayerDto> GoldenSix);

/// <summary>Per-player detail in the Golden Six section of the standings response.</summary>
public sealed record GoldenSixPlayerDto(
    Guid PlayerId,
    string Name,
    string TeamId,
    string Position,
    int Goals,
    int Points);
