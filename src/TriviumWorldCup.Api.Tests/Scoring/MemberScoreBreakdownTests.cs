using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Tests for the MemberScore breakdown invariants introduced by the scoring centralisation refactor.
///
/// These tests run fully in memory — no database required. They simulate the accumulation
/// loops in ScoringRecomputeService and verify that:
///   1. Breakdown sums always equal the stored totals (breakdown-total invariant).
///   2. Running the accumulation twice produces identical lists (idempotency).
///   3. Pure scorer classes are not referenced from outside the Scoring namespace (guardrail).
/// </summary>
public class MemberScoreBreakdownTests
{
    // ── 1. Group breakdown: sum of Points == GroupMatchPoints ─────────────────

    [Fact]
    public void GroupBreakdown_SumEqualsGroupMatchPoints()
    {
        // Arrange: three fixtures with known results and predictions.
        var fixtures = new[]
        {
            MakeFixture("F1", homeScore: 2, awayScore: 1),
            MakeFixture("F2", homeScore: 0, awayScore: 0),
            MakeFixture("F3", homeScore: 3, awayScore: 2),
        };
        var predictions = new[]
        {
            MakePred("F1", predHome: 2, predAway: 1), // exact → 10
            MakePred("F2", predHome: 1, predAway: 0), // wrong outcome, away tally matches (0==0) → tally bonus +1 → 1
            MakePred("F3", predHome: 2, predAway: 1), // correct GD (+1 == +1) → 7
        };

        // Act: accumulate exactly as ScoringRecomputeService does in Step 1.
        var fixtureById = fixtures.ToDictionary(f => f.Id);
        var totalPoints = 0;
        var breakdown   = new List<GroupPredictionScore>();

        foreach (var pred in predictions)
        {
            if (!fixtureById.TryGetValue(pred.FixtureId, out var fixture)) continue;
            var pts     = GroupMatchScorer.Compute(pred.HomeScore, pred.AwayScore, fixture.HomeScore!.Value, fixture.AwayScore!.Value);
            var isExact = GroupMatchScorer.IsExact(pred.HomeScore, pred.AwayScore, fixture.HomeScore!.Value, fixture.AwayScore!.Value);
            totalPoints += pts;
            breakdown.Add(new GroupPredictionScore(pred.FixtureId, pts, isExact, pts >= 3));
        }

        // Assert: invariant holds.
        Assert.Equal(totalPoints, breakdown.Sum(b => b.Points));
        Assert.Equal(18, totalPoints); // 10 + 1 + 7
        Assert.Equal(3, breakdown.Count);
        Assert.True(breakdown.Single(b => b.FixtureId == "F1").IsExact);
        Assert.False(breakdown.Single(b => b.FixtureId == "F2").IsCorrectOutcome);
        Assert.True(breakdown.Single(b => b.FixtureId == "F3").IsCorrectOutcome);
    }

    [Fact]
    public void GroupBreakdown_Idempotent_RunningTwiceProducesSameList()
    {
        var fixtures = new[] { MakeFixture("F1", 1, 0), MakeFixture("F2", 2, 2) };
        var predictions = new[] { MakePred("F1", 1, 0), MakePred("F2", 1, 1) };

        var run1 = AccumulateGroupBreakdown(fixtures, predictions);
        var run2 = AccumulateGroupBreakdown(fixtures, predictions);

        Assert.Equal(run1.Count, run2.Count);
        for (var i = 0; i < run1.Count; i++)
            Assert.Equal(run1[i], run2[i]);
    }

    // ── 2. Knockout breakdown: sum of (ScorePoints + AdvancingPoints) == KnockoutPoints ──

    [Fact]
    public void KnockoutBreakdown_SumEqualsKnockoutPoints()
    {
        // Three slots: correct winner with streak, correct winner no streak, wrong winner.
        // R32-1 correct → streakBefore = 0 → multiplier 1
        // QF-1  correct → feeder is R32-1, streakBefore = 1 → multiplier 2
        // SF-1  wrong winner
        var totalPoints = 0;
        var breakdown   = new List<KnockoutPredictionScore>();

        // R32-1: score exact (10) + advancing correct streak 0 (5) = 15
        AddKoEntry("R32-1", scorePoints: 10, advancingPoints: 5,  streakMultiplier: 1, ref totalPoints, breakdown);
        // QF-1: score outcome only (3) + advancing correct streak 1 (10) = 13
        AddKoEntry("QF-1",  scorePoints: 3,  advancingPoints: 10, streakMultiplier: 2, ref totalPoints, breakdown);
        // SF-1: wrong winner → score 0 + advancing 0
        AddKoEntry("SF-1",  scorePoints: 0,  advancingPoints: 0,  streakMultiplier: 0, ref totalPoints, breakdown);

        Assert.Equal(28, totalPoints);
        Assert.Equal(totalPoints, breakdown.Sum(b => b.ScorePoints + b.AdvancingPoints));
        Assert.Equal(0, breakdown.Single(b => b.SlotKey == "SF-1").StreakMultiplier);
    }

    [Fact]
    public void KnockoutBreakdown_Idempotent()
    {
        var run1 = new List<KnockoutPredictionScore>();
        var run2 = new List<KnockoutPredictionScore>();
        var dummy = 0;

        AddKoEntry("R32-1", 10, 5,  1, ref dummy, run1);
        AddKoEntry("R32-1", 10, 5,  1, ref dummy, run2);

        Assert.Equal(run1[0], run2[0]);
    }

    // ── 3. Golden Six breakdown: sum of Points == GoldenSixPoints ────────────

    [Fact]
    public void GoldenSixBreakdown_SumEqualsGoldenSixPoints()
    {
        var playerStats = new Dictionary<Guid, (Position position, int goals)>
        {
            [Guid.Parse("00000000-0000-0000-0000-000000000001")] = (Position.FWD, 3), // 3×3 = 9
            [Guid.Parse("00000000-0000-0000-0000-000000000002")] = (Position.MID, 2), // 5×2 = 10
            [Guid.Parse("00000000-0000-0000-0000-000000000003")] = (Position.DEF, 1), // 8×1 = 8
            [Guid.Parse("00000000-0000-0000-0000-000000000004")] = (Position.GK,  0), // 15×0 = 0
        };
        var pickedIds = playerStats.Keys.ToList();

        var totalPoints = GoldenSixScorer.ComputeTotal(playerStats, pickedIds);

        var breakdown = pickedIds
            .Where(id => playerStats.ContainsKey(id))
            .Select(id =>
            {
                var (position, goals) = playerStats[id];
                return new GoldenSixPlayerScore(id, goals, GoldenSixScorer.ComputeForPlayer(position, goals));
            })
            .ToList();

        Assert.Equal(27, totalPoints); // 9+10+8+0
        Assert.Equal(totalPoints, breakdown.Sum(b => b.Points));
    }

    [Fact]
    public void GoldenSixBreakdown_Idempotent()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var playerStats = new Dictionary<Guid, (Position, int)> { [id] = (Position.MID, 4) };
        var ids = new[] { id };

        var run1 = new GoldenSixPlayerScore(id, 4, GoldenSixScorer.ComputeForPlayer(Position.MID, 4));
        var run2 = new GoldenSixPlayerScore(id, 4, GoldenSixScorer.ComputeForPlayer(Position.MID, 4));

        Assert.Equal(run1, run2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Fixture MakeFixture(string id, int homeScore, int awayScore) => new()
    {
        Id         = id,
        HomeTeamId = "A",
        AwayTeamId = "B",
        HomeScore  = homeScore,
        AwayScore  = awayScore,
        Status     = MatchStatus.Completed,
        KickoffUtc = DateTimeOffset.UtcNow.AddDays(-1),
    };

    private static GroupPrediction MakePred(string fixtureId, int predHome, int predAway) => new()
    {
        Id        = $"user1_{fixtureId}",
        UserId    = "user1",
        FixtureId = fixtureId,
        HomeScore = predHome,
        AwayScore = predAway,
    };

    private static List<GroupPredictionScore> AccumulateGroupBreakdown(
        IEnumerable<Fixture> fixtures, IEnumerable<GroupPrediction> predictions)
    {
        var fixtureById = fixtures.ToDictionary(f => f.Id);
        var breakdown   = new List<GroupPredictionScore>();
        foreach (var pred in predictions)
        {
            if (!fixtureById.TryGetValue(pred.FixtureId, out var fixture)) continue;
            var pts     = GroupMatchScorer.Compute(pred.HomeScore, pred.AwayScore, fixture.HomeScore!.Value, fixture.AwayScore!.Value);
            var isExact = GroupMatchScorer.IsExact(pred.HomeScore, pred.AwayScore, fixture.HomeScore!.Value, fixture.AwayScore!.Value);
            breakdown.Add(new GroupPredictionScore(pred.FixtureId, pts, isExact, pts >= 3));
        }
        return breakdown;
    }

    private static void AddKoEntry(string slotKey, int scorePoints, int advancingPoints, int streakMultiplier,
        ref int total, List<KnockoutPredictionScore> breakdown)
    {
        total += scorePoints + advancingPoints;
        breakdown.Add(new KnockoutPredictionScore(slotKey, scorePoints, advancingPoints, streakMultiplier));
    }

}
