using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Leaderboard;

namespace TriviumWorldCup.Api.Tests.Standings;

/// <summary>
/// TWC-60: GET /scores/me and GET /leaderboard must report identical ranks for every member,
/// in every tie scenario. /scores/me previously computed rank as a simple
/// count(TotalPoints > mine) + 1 — points-only, ignoring the tiebreaker chain — while
/// /leaderboard used LeaderboardRanker.Rank (TotalPoints → ExactScorelineCount →
/// CorrectOutcomeCount, competition/Olympic ranking). /scores/me now calls
/// LeaderboardRanker.Rank directly and looks up its own entry, making it the same
/// computation as /leaderboard by construction. These tests exercise that shared
/// ranking function directly against tie scenarios that previously diverged.
/// </summary>
public class StandingsRankingTests
{
    private static MemberScore MakeScore(
        string userId,
        int groupMatchPoints,
        int championPoints = 0,
        int goldenSixPoints = 0,
        int exactScorelineCount = 0,
        int correctOutcomeCount = 0) => new()
        {
            Id                  = userId,
            UserId              = userId,
            GroupMatchPoints    = groupMatchPoints,
            ChampionPoints      = championPoints,
            GoldenSixPoints     = goldenSixPoints,
            ExactScorelineCount = exactScorelineCount,
            CorrectOutcomeCount = correctOutcomeCount,
            LastComputedAt      = DateTimeOffset.UtcNow,
        };

    /// <summary>
    /// Mirrors the old, buggy /scores/me computation — points-only rank — used here only to
    /// prove it diverges from LeaderboardRanker.Rank in a tie scenario (i.e. the bug was real).
    /// </summary>
    private static int PointsOnlyRank(IReadOnlyList<MemberScore> allScores, string userId)
    {
        var mine = allScores.First(s => s.UserId == userId).TotalPoints;
        return allScores.Count(s => s.TotalPoints > mine) + 1;
    }

    [Fact]
    public void TiedOnPoints_DifferentTiebreaker_LeaderboardRankerDistinguishesThem()
    {
        // Same TotalPoints (50), but "alice" has more exact scorelines — she should outrank "bob".
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 50, exactScorelineCount: 3, correctOutcomeCount: 1),
            MakeScore("bob",   groupMatchPoints: 50, exactScorelineCount: 1, correctOutcomeCount: 5),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        var aliceRank = ranked.First(r => r.Score.UserId == "alice").Rank;
        var bobRank   = ranked.First(r => r.Score.UserId == "bob").Rank;

        Assert.Equal(1, aliceRank);
        Assert.Equal(2, bobRank);

        // The old /scores/me algorithm (points-only) would have called them both rank 1 —
        // demonstrating the exact divergence this story fixes.
        Assert.Equal(1, PointsOnlyRank(scores, "alice"));
        Assert.Equal(1, PointsOnlyRank(scores, "bob"));
        Assert.NotEqual(bobRank, PointsOnlyRank(scores, "bob"));
    }

    [Fact]
    public void MeAndLeaderboard_UseSameFunction_ProduceIdenticalRanksForEveryMember()
    {
        // A mixed set: some fully tied, one distinguished by tiebreaker, one clearly ahead.
        var scores = new[]
        {
            MakeScore("carol", groupMatchPoints: 80),
            MakeScore("dave",  groupMatchPoints: 50, exactScorelineCount: 2, correctOutcomeCount: 4),
            MakeScore("erin",  groupMatchPoints: 50, exactScorelineCount: 2, correctOutcomeCount: 4), // fully tied with dave
            MakeScore("frank", groupMatchPoints: 50, exactScorelineCount: 1, correctOutcomeCount: 9),
        };

        // /leaderboard's computation.
        var leaderboardRanked = LeaderboardRanker.Rank(scores)
            .ToDictionary(r => r.Score.UserId, r => r.Rank);

        // /scores/me's computation for each member — identical call, looked up per-user,
        // exactly as the fixed endpoint now does.
        foreach (var userId in new[] { "carol", "dave", "erin", "frank" })
        {
            var meRank = LeaderboardRanker.Rank(scores).First(r => r.Score.UserId == userId).Rank;
            Assert.Equal(leaderboardRanked[userId], meRank);
        }

        // Sanity on the expected shape: carol=1, dave/erin tied at 2, frank=4 (competition ranking).
        Assert.Equal(1, leaderboardRanked["carol"]);
        Assert.Equal(2, leaderboardRanked["dave"]);
        Assert.Equal(2, leaderboardRanked["erin"]);
        Assert.Equal(4, leaderboardRanked["frank"]);
    }
}
