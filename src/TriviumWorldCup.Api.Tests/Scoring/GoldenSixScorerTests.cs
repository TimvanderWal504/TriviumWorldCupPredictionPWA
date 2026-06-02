using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Unit tests for GoldenSixScorer — position points, per-player compute, total compute.
/// No database required; pure function inputs/outputs.
/// TWC-8 acceptance criteria: PointsPerGoal for each position, ComputeForPlayer,
/// ComputeTotal including edge cases.
/// </summary>
public class GoldenSixScorerTests
{
    // ── PointsPerGoal ─────────────────────────────────────────────────────────

    [Fact]
    public void PointsPerGoal_Forward_ReturnsThree()
    {
        Assert.Equal(3, GoldenSixScorer.PointsPerGoal(Position.FWD));
    }

    [Fact]
    public void PointsPerGoal_Midfielder_ReturnsFive()
    {
        Assert.Equal(5, GoldenSixScorer.PointsPerGoal(Position.MID));
    }

    [Fact]
    public void PointsPerGoal_Defender_ReturnsEight()
    {
        Assert.Equal(8, GoldenSixScorer.PointsPerGoal(Position.DEF));
    }

    [Fact]
    public void PointsPerGoal_Goalkeeper_ReturnsFifteen()
    {
        Assert.Equal(15, GoldenSixScorer.PointsPerGoal(Position.GK));
    }

    // ── ComputeForPlayer ──────────────────────────────────────────────────────

    [Fact]
    public void ComputeForPlayer_TwoGoals_Midfielder_ReturnsTen()
    {
        Assert.Equal(10, GoldenSixScorer.ComputeForPlayer(Position.MID, 2));
    }

    [Fact]
    public void ComputeForPlayer_ZeroGoals_ReturnsZero()
    {
        Assert.Equal(0, GoldenSixScorer.ComputeForPlayer(Position.FWD, 0));
    }

    [Fact]
    public void ComputeForPlayer_OneGoal_Forward_ReturnsThree()
    {
        Assert.Equal(3, GoldenSixScorer.ComputeForPlayer(Position.FWD, 1));
    }

    [Fact]
    public void ComputeForPlayer_OneGoal_Goalkeeper_ReturnsFifteen()
    {
        Assert.Equal(15, GoldenSixScorer.ComputeForPlayer(Position.GK, 1));
    }

    [Fact]
    public void ComputeForPlayer_ThreeGoals_Defender_ReturnsTwentyFour()
    {
        Assert.Equal(24, GoldenSixScorer.ComputeForPlayer(Position.DEF, 3));
    }

    // ── ComputeTotal ──────────────────────────────────────────────────────────

    [Fact]
    public void ComputeTotal_MixOfPositionsAndGoals_ReturnsCorrectSum()
    {
        var fwdId  = Guid.NewGuid();
        var midId  = Guid.NewGuid();
        var defId  = Guid.NewGuid();
        var gkId   = Guid.NewGuid();

        var stats = new Dictionary<Guid, (Position, int)>
        {
            { fwdId,  (Position.FWD, 2) },  // 2 × 3 = 6
            { midId,  (Position.MID, 1) },  // 1 × 5 = 5
            { defId,  (Position.DEF, 1) },  // 1 × 8 = 8
            { gkId,   (Position.GK,  1) },  // 1 × 15 = 15
        };

        var picks = new[] { fwdId, midId, defId, gkId };

        // 6 + 5 + 8 + 15 = 34
        Assert.Equal(34, GoldenSixScorer.ComputeTotal(stats, picks));
    }

    [Fact]
    public void ComputeTotal_PlayerNotInPicks_IsNotCounted()
    {
        var pickedId    = Guid.NewGuid();
        var notPickedId = Guid.NewGuid();

        var stats = new Dictionary<Guid, (Position, int)>
        {
            { pickedId,    (Position.FWD, 2) }, // 2 × 3 = 6
            { notPickedId, (Position.MID, 5) }, // should NOT be counted
        };

        var picks = new[] { pickedId };

        Assert.Equal(6, GoldenSixScorer.ComputeTotal(stats, picks));
    }

    [Fact]
    public void ComputeTotal_PlayerPickedButZeroGoals_ReturnsZero()
    {
        var playerId = Guid.NewGuid();

        var stats = new Dictionary<Guid, (Position, int)>
        {
            { playerId, (Position.FWD, 0) },
        };

        var picks = new[] { playerId };

        Assert.Equal(0, GoldenSixScorer.ComputeTotal(stats, picks));
    }

    [Fact]
    public void ComputeTotal_PlayerPickedButNotInStats_ReturnsZero()
    {
        // Player was picked but has no goal events recorded (0 goals implied).
        var pickedId = Guid.NewGuid();
        var stats    = new Dictionary<Guid, (Position, int)>(); // empty

        var picks = new[] { pickedId };

        Assert.Equal(0, GoldenSixScorer.ComputeTotal(stats, picks));
    }

    [Fact]
    public void ComputeTotal_SamePlayerPickedByTwoMembers_CountedIndependently()
    {
        // Two members can pick the same player; each member's points are computed
        // from their own picks — not a shared pool.
        var playerId = Guid.NewGuid();

        var stats = new Dictionary<Guid, (Position, int)>
        {
            { playerId, (Position.MID, 3) }, // 3 × 5 = 15
        };

        // Member A picks that player
        var member1Picks = new[] { playerId };
        // Member B also picks that player
        var member2Picks = new[] { playerId };

        Assert.Equal(15, GoldenSixScorer.ComputeTotal(stats, member1Picks));
        Assert.Equal(15, GoldenSixScorer.ComputeTotal(stats, member2Picks));
    }

    [Fact]
    public void ComputeTotal_EmptyPicks_ReturnsZero()
    {
        var stats = new Dictionary<Guid, (Position, int)>
        {
            { Guid.NewGuid(), (Position.FWD, 5) },
        };

        Assert.Equal(0, GoldenSixScorer.ComputeTotal(stats, Array.Empty<Guid>()));
    }

    [Fact]
    public void ComputeTotal_EmptyStats_ReturnsZero()
    {
        var picks = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var stats = new Dictionary<Guid, (Position, int)>();

        Assert.Equal(0, GoldenSixScorer.ComputeTotal(stats, picks));
    }

    [Fact]
    public void ComputeTotal_SixForwardsThreeGoalsEach_Returns54()
    {
        var picks = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray();
        var stats = picks.ToDictionary(id => id, _ => (Position.FWD, 3));

        // 6 players × 3 goals × 3 pts = 54
        Assert.Equal(54, GoldenSixScorer.ComputeTotal(stats, picks));
    }
}
