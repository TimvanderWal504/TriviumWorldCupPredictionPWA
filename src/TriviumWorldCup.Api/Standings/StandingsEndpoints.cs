using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Leaderboard;

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

            // Rank — single source of truth: LeaderboardRanker.Rank (same TotalPoints →
            // ExactScorelineCount → CorrectOutcomeCount tiebreaker chain and competition/Olympic
            // ranking used by /leaderboard), so ranks are always identical between the two endpoints.
            int rank;
            if (myScore is not null)
            {
                var ranked = LeaderboardRanker.Rank(allScores);
                rank = ranked.First(rs => rs.Score.UserId == user.UserId).Rank;
            }
            else
            {
                // No MemberScore yet — same "unscored members at the bottom" rank /leaderboard uses.
                rank = allScores.Count + 1;
            }

            var totalMembers = allScores.Count;
            var myTotalPoints = myScore?.TotalPoints ?? 0;

            // Load tournament prediction to get Golden Six picks.
            var tournamentPrediction = await session.LoadAsync<TournamentPrediction>(user.UserId, ct);

            var goldenSixItems = new List<GoldenSixPlayerDto>();

            if (tournamentPrediction is not null && tournamentPrediction.GoldenSixPlayerIds.Count > 0)
            {
                var playerIds = tournamentPrediction.GoldenSixPlayerIds;

                // Load all 6 player documents for name, teamId, and position.
                var players = await session
                    .Query<Player>()
                    .Where(p => p.Id.IsOneOf(playerIds))
                    .ToListAsync(ct);

                var playerById = players.ToDictionary(p => p.Id);

                var gs6BreakdownById = myScore?.GoldenSixBreakdown.ToDictionary(b => b.PlayerId)
                                        ?? new Dictionary<Guid, GoldenSixPlayerScore>();

                foreach (var playerId in playerIds)
                {
                    if (!playerById.TryGetValue(playerId, out var player))
                        continue; // player document missing — skip

                    gs6BreakdownById.TryGetValue(playerId, out var gs6);

                    goldenSixItems.Add(new GoldenSixPlayerDto(
                        PlayerId: playerId,
                        Name:     player.Name,
                        TeamId:   player.TeamId,
                        Position: player.Position.ToString(),
                        Goals:    gs6?.Goals ?? 0,
                        Points:   gs6?.Points ?? 0));
                }
            }

            var response = new MyStandingsDto(
                UserId: user.UserId,
                TotalPoints: myTotalPoints,
                GroupMatchPoints: myScore?.GroupMatchPoints ?? 0,
                KnockoutPoints: myScore?.KnockoutPoints ?? 0,
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
    int KnockoutPoints,
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
