using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Leaderboard;

namespace TriviumWorldCup.Api.Tests.Leaderboard;

/// <summary>
/// Unit tests for LeaderboardRanker — competition ranking with the tiebreaker chain.
/// Pure function; no database required.
///
/// Tiebreaker chain (TWC-11 canonical):
///   1. TotalPoints DESC (= GroupMatchPoints + ChampionPoints + GoldenSixPoints)
///   2. ExactScorelineCount DESC
///   3. CorrectOutcomeCount DESC
///   4. Still equal → shared rank (competition / Olympic ranking — not dense)
/// </summary>
public class LeaderboardRankingTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal MemberScore with only the fields relevant to ranking.
    /// GroupMatchPoints is used as TotalPoints proxy since TotalPoints is computed.
    /// </summary>
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

    // ── Single member ─────────────────────────────────────────────────────────

    [Fact]
    public void SingleMember_AssignedRankOne()
    {
        var scores = new[] { MakeScore("alice", groupMatchPoints: 50) };
        var ranked = LeaderboardRanker.Rank(scores);

        Assert.Single(ranked);
        Assert.Equal(1, ranked[0].Rank);
        Assert.Equal("alice", ranked[0].Score.UserId);
    }

    // ── Two members, different total points ──────────────────────────────────

    [Fact]
    public void TwoMembers_DifferentTotals_CorrectRanks()
    {
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 85),
            MakeScore("bob",   groupMatchPoints: 72),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        // Alice scores more → rank 1; Bob → rank 2.
        Assert.Equal(2, ranked.Count);

        var alice = ranked.Single(r => r.Score.UserId == "alice");
        var bob   = ranked.Single(r => r.Score.UserId == "bob");

        Assert.Equal(1, alice.Rank);
        Assert.Equal(2, bob.Rank);
    }

    [Fact]
    public void TwoMembers_DifferentTotals_LowerScorerHasHigherRankNumber()
    {
        var scores = new[]
        {
            MakeScore("lower", groupMatchPoints: 10),
            MakeScore("higher", groupMatchPoints: 50),
        };

        var ranked = LeaderboardRanker.Rank(scores);
        var higher = ranked.Single(r => r.Score.UserId == "higher");
        var lower  = ranked.Single(r => r.Score.UserId == "lower");

        Assert.True(higher.Rank < lower.Rank);
    }

    // ── Tiebreaker 1: ExactScorelineCount ────────────────────────────────────

    [Fact]
    public void SameTotalPoints_DifferentExactCounts_HigherExactCountWins()
    {
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 85, exactScorelineCount: 3),
            MakeScore("bob",   groupMatchPoints: 85, exactScorelineCount: 5),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        var alice = ranked.Single(r => r.Score.UserId == "alice");
        var bob   = ranked.Single(r => r.Score.UserId == "bob");

        // Bob has more exact scorelines → rank 1; Alice → rank 2.
        Assert.Equal(1, bob.Rank);
        Assert.Equal(2, alice.Rank);
    }

    // ── Tiebreaker 2: CorrectOutcomeCount ────────────────────────────────────

    [Fact]
    public void SameTotalsAndExactCounts_DifferentOutcomeCounts_HigherOutcomeCountWins()
    {
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 7),
            MakeScore("bob",   groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 4),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        var alice = ranked.Single(r => r.Score.UserId == "alice");
        var bob   = ranked.Single(r => r.Score.UserId == "bob");

        // Alice has more correct outcomes → rank 1; Bob → rank 2.
        Assert.Equal(1, alice.Rank);
        Assert.Equal(2, bob.Rank);
    }

    // ── Full tie → shared rank ────────────────────────────────────────────────

    [Fact]
    public void TwoMembersIdenticalOnAllThree_BothRankOne()
    {
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 7),
            MakeScore("bob",   groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 7),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        Assert.Equal(2, ranked.Count);
        Assert.All(ranked, r => Assert.Equal(1, r.Rank));
    }

    [Fact]
    public void TwoMembersIdenticalOnAllThree_NextMemberWouldBeRankThree()
    {
        // Competition ranking: two tied at rank 1 → next open rank is 3.
        // We verify this by adding a third member with lower points.
        var scores = new[]
        {
            MakeScore("alice",   groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 7),
            MakeScore("bob",     groupMatchPoints: 85, exactScorelineCount: 3, correctOutcomeCount: 7),
            MakeScore("charlie", groupMatchPoints: 72),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        var charlie = ranked.Single(r => r.Score.UserId == "charlie");
        Assert.Equal(3, charlie.Rank);
    }

    // ── Three members: two tied at rank 1, one at rank 3 ─────────────────────

    [Fact]
    public void ThreeMembers_TwoTiedAtRankOne_ThirdIsRankThree()
    {
        var scores = new[]
        {
            MakeScore("alice",   groupMatchPoints: 100),
            MakeScore("bob",     groupMatchPoints: 100),
            MakeScore("charlie", groupMatchPoints: 60),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        Assert.Equal(3, ranked.Count);

        var alice   = ranked.Single(r => r.Score.UserId == "alice");
        var bob     = ranked.Single(r => r.Score.UserId == "bob");
        var charlie = ranked.Single(r => r.Score.UserId == "charlie");

        Assert.Equal(1, alice.Rank);
        Assert.Equal(1, bob.Rank);
        Assert.Equal(3, charlie.Rank);
    }

    // ── Empty input ───────────────────────────────────────────────────────────

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var ranked = LeaderboardRanker.Rank(Array.Empty<MemberScore>());
        Assert.Empty(ranked);
    }

    // ── TotalPoints is computed (GroupMatchPoints + ChampionPoints + GoldenSixPoints) ──

    [Fact]
    public void TotalPoints_IncludesAllCategories()
    {
        // TotalPoints is a computed property: GroupMatch + Champion + GoldenSix.
        // Member A has 60 group + 100 champ = 160 total.
        // Member B has 85 group + 0 champ = 85 total.
        var scores = new[]
        {
            MakeScore("alice", groupMatchPoints: 60, championPoints: 100),
            MakeScore("bob",   groupMatchPoints: 85, championPoints: 0),
        };

        var ranked = LeaderboardRanker.Rank(scores);

        var alice = ranked.Single(r => r.Score.UserId == "alice");
        var bob   = ranked.Single(r => r.Score.UserId == "bob");

        Assert.Equal(1, alice.Rank);
        Assert.Equal(2, bob.Rank);
    }
}
