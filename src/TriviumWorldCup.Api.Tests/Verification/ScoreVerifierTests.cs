using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;
using TriviumWorldCup.Api.Verification;

namespace TriviumWorldCup.Api.Tests.Verification;

/// <summary>
/// Tests for the independent points verifier.
///
/// The verifier is a second implementation of the scoring rules that never calls the
/// production scorers. The most valuable tests here are therefore the cross-check ones:
/// they compute the "stored" MemberScore using the production scorers and assert the
/// verifier reports a clean run. If the two implementations ever drift, these fail.
///
/// Runs fully in memory via ScoreVerifier.BuildReport — no database required.
/// </summary>
public class ScoreVerifierTests
{
    // ── Cross-check: production scorers vs independent derivation ─────────────

    [Fact]
    public void GroupPredictions_ScoredByProductionScorer_VerifyReportsClean()
    {
        var fixtures = new[]
        {
            MakeFixture("F1", 2, 1),
            MakeFixture("F2", 0, 0),
            MakeFixture("F3", 3, 2),
            MakeFixture("F4", 1, 4),
        };
        var preds = new[]
        {
            MakeGroupPred("u1", "F1", 2, 1), // exact                → 10
            MakeGroupPred("u1", "F2", 1, 0), // wrong, away tally ok → 1
            MakeGroupPred("u1", "F3", 2, 1), // correct GD           → 7
            MakeGroupPred("u1", "F4", 0, 3), // correct GD           → 7
        };

        var stored = ScoreUsingProductionScorers("u1", fixtures, preds);

        var report = Run(fixtures: fixtures, groupPreds: preds, storedScores: [stored]);

        Assert.Empty(report.Users);
        Assert.Equal(25, stored.GroupMatchPoints); // 10 + 1 + 7 + 7
    }

    [Fact]
    public void KnockoutStreak_ScoredByProductionScorer_VerifyReportsClean()
    {
        // NED advances R32-1 → R16-1 → QF-1, user predicts it correctly each time.
        // Expected multipliers along the path: x1, x2, x3.
        var slots = BracketPath();
        var preds = new[]
        {
            MakeKoPred("u1", "R32-1", "NED", 2, 0),
            MakeKoPred("u1", "R16-1", "NED", 1, 0),
            MakeKoPred("u1", "QF-1",  "NED", 1, 1), // score wrong, winner right
        };

        var stored = ScoreKnockoutUsingProductionScorers("u1", slots, preds);

        var report = Run(knockoutSlots: slots, knockoutPreds: preds, storedScores: [stored]);

        Assert.Empty(report.Users);

        // Sanity-check the multipliers the production path actually produced.
        Assert.Equal(1, stored.KnockoutBreakdown.Single(b => b.SlotKey == "R32-1").StreakMultiplier);
        Assert.Equal(2, stored.KnockoutBreakdown.Single(b => b.SlotKey == "R16-1").StreakMultiplier);
        Assert.Equal(3, stored.KnockoutBreakdown.Single(b => b.SlotKey == "QF-1").StreakMultiplier);
    }

    [Fact]
    public void KnockoutStreak_BreaksWhenAnEarlierPickWasWrong()
    {
        // User gets R32-1 wrong (picks BEL, NED advance), then R16-1 and QF-1 right.
        // The streak follows the team's path, so R16-1 restarts at x1 and QF-1 is x2.
        var slots = BracketPath();
        var preds = new[]
        {
            MakeKoPred("u1", "R32-1", "BEL", 0, 1),
            MakeKoPred("u1", "R16-1", "NED", 1, 0),
            MakeKoPred("u1", "QF-1",  "NED", 2, 1),
        };

        var stored = ScoreKnockoutUsingProductionScorers("u1", slots, preds);
        var report = Run(knockoutSlots: slots, knockoutPreds: preds, storedScores: [stored]);

        Assert.Empty(report.Users);
        Assert.Equal(0, stored.KnockoutBreakdown.Single(b => b.SlotKey == "R32-1").StreakMultiplier);
        Assert.Equal(1, stored.KnockoutBreakdown.Single(b => b.SlotKey == "R16-1").StreakMultiplier);
        Assert.Equal(2, stored.KnockoutBreakdown.Single(b => b.SlotKey == "QF-1").StreakMultiplier);
    }

    [Fact]
    public void GoldenSix_ScoredByProductionScorer_VerifyReportsClean()
    {
        var gk  = MakePlayer(Position.GK);
        var def = MakePlayer(Position.DEF);
        var fwd = MakePlayer(Position.FWD);

        var goals = new[]
        {
            MakeGoal(gk.Id,  GoalType.OpenPlay),
            MakeGoal(def.Id, GoalType.OpenPlay),
            MakeGoal(def.Id, GoalType.PenaltyInMatch),
            MakeGoal(fwd.Id, GoalType.Shootout), // must not count
            MakeGoal(fwd.Id, GoalType.OwnGoal),  // must not count
        };

        var tp = new TournamentPrediction
        {
            Id = "u1", UserId = "u1",
            GoldenSixPlayerIds = [gk.Id, def.Id, fwd.Id],
        };

        var stored = new MemberScore
        {
            Id = "u1", UserId = "u1",
            GoldenSixPoints = 15 + 16, // GK 1 goal x15, DEF 2 goals x8, FWD 0
            GoldenSixBreakdown =
            [
                new GoldenSixPlayerScore(gk.Id,  1, 15),
                new GoldenSixPlayerScore(def.Id, 2, 16),
                new GoldenSixPlayerScore(fwd.Id, 0, 0),
            ],
        };

        var report = Run(
            players: [gk, def, fwd],
            goals: goals,
            tournamentPreds: [tp],
            storedScores: [stored]);

        Assert.Empty(report.Users);
    }

    // ── Detection: the verifier must catch corrupted stored scores ────────────

    [Fact]
    public void TamperedTotal_IsReportedWithDelta()
    {
        var fixtures = new[] { MakeFixture("F1", 2, 1) };
        var preds    = new[] { MakeGroupPred("u1", "F1", 2, 1) }; // exact → 10

        var stored = ScoreUsingProductionScorers("u1", fixtures, preds);
        stored.GroupMatchPoints = 7; // corrupt

        var report = Run(fixtures: fixtures, groupPreds: preds, storedScores: [stored]);

        var user = Assert.Single(report.Users);
        Assert.Equal("u1", user.UserId);

        var groupDiff = user.Totals.Single(t => t.Field == "GroupMatchPoints");
        Assert.Equal(7,  groupDiff.Stored);
        Assert.Equal(10, groupDiff.Expected);
        Assert.Equal(3,  groupDiff.Delta);
    }

    [Fact]
    public void BreakdownThatDisagreesWithItsOwnTotal_IsReported()
    {
        // Total is right, but the per-prediction breakdown the leaderboard reads is not.
        var fixtures = new[] { MakeFixture("F1", 2, 1) };
        var preds    = new[] { MakeGroupPred("u1", "F1", 2, 1) };

        var stored = ScoreUsingProductionScorers("u1", fixtures, preds);
        stored.GroupBreakdown = [new GroupPredictionScore("F1", 3, false, true)];

        var report = Run(fixtures: fixtures, groupPreds: preds, storedScores: [stored]);

        var user = Assert.Single(report.Users);
        Assert.Contains(user.Totals, t => t.Field == "GroupBreakdownSum");
        Assert.Contains(user.Predictions, p => p.Kind == "group" && p.Key == "F1" && p.Expected == 10);
    }

    [Fact]
    public void StaleScore_MissingARecentlyCompletedFixture_IsReported()
    {
        // Simulates the real failure mode: a result landed but no recompute ran.
        var fixtures = new[] { MakeFixture("F1", 2, 1), MakeFixture("F2", 1, 0) };
        var preds    = new[]
        {
            MakeGroupPred("u1", "F1", 2, 1),
            MakeGroupPred("u1", "F2", 1, 0),
        };

        // Stored score only knows about F1.
        var stored = ScoreUsingProductionScorers("u1", [fixtures[0]], [preds[0]]);

        var report = Run(fixtures: fixtures, groupPreds: preds, storedScores: [stored]);

        var user = Assert.Single(report.Users);
        Assert.Contains(user.Predictions, p => p.Key == "F2" && p.Expected == 10);
        Assert.Contains(user.Totals, t => t.Field == "GroupMatchPoints" && t.Delta == 10);
    }

    [Fact]
    public void MemberWithPointsButNoScoreDocument_IsReported()
    {
        var fixtures = new[] { MakeFixture("F1", 2, 1) };
        var preds    = new[] { MakeGroupPred("u1", "F1", 2, 1) };

        var report = Run(fixtures: fixtures, groupPreds: preds, storedScores: []);

        var user = Assert.Single(report.Users);
        Assert.True(user.MissingScoreDocument);
    }

    [Fact]
    public void ScoreDocumentWithPointsButNoPredictions_IsReportedAsOrphan()
    {
        var stored = new MemberScore { Id = "ghost", UserId = "ghost", GroupMatchPoints = 42 };

        var report = Run(storedScores: [stored]);

        var user = Assert.Single(report.Users);
        Assert.True(user.OrphanedScoreDocument);
    }

    [Fact]
    public void MemberWithNoPredictionsAndZeroScore_IsNotReported()
    {
        var stored = new MemberScore { Id = "u1", UserId = "u1" };

        var report = Run(storedScores: [stored]);

        Assert.Empty(report.Users);
        Assert.Equal(1, report.UsersChecked);
    }

    [Fact]
    public void UnscoredFixtures_AreNotCountedAgainstTheMember()
    {
        // Prediction exists but the fixture has no result yet — must not be flagged.
        var preds = new[] { MakeGroupPred("u1", "F9", 1, 0) };
        var stored = new MemberScore { Id = "u1", UserId = "u1" };

        var report = Run(groupPreds: preds, storedScores: [stored]);

        Assert.Empty(report.Users);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ScoreVerificationReport Run(
        IReadOnlyList<Fixture>? fixtures = null,
        IReadOnlyList<KnockoutSlot>? knockoutSlots = null,
        IReadOnlyList<Player>? players = null,
        IReadOnlyList<GoalEvent>? goals = null,
        IReadOnlyList<GroupPrediction>? groupPreds = null,
        IReadOnlyList<KnockoutPrediction>? knockoutPreds = null,
        IReadOnlyList<TournamentPrediction>? tournamentPreds = null,
        IReadOnlyList<MemberScore>? storedScores = null)
        => ScoreVerifier.BuildReport(
            fixtures        ?? [],
            knockoutSlots   ?? [],
            players         ?? [],
            goals           ?? [],
            groupPreds      ?? [],
            knockoutPreds   ?? [],
            tournamentPreds ?? [],
            storedScores    ?? []);

    /// <summary>
    /// Builds the MemberScore the production pipeline would write for group-stage
    /// predictions, using the real scorer. This is the "known good" side of the cross-check.
    /// </summary>
    private static MemberScore ScoreUsingProductionScorers(
        string userId,
        IReadOnlyList<Fixture> fixtures,
        IReadOnlyList<GroupPrediction> preds)
    {
        var byId = fixtures.ToDictionary(f => f.Id);
        var score = new MemberScore { Id = userId, UserId = userId };

        foreach (var p in preds.Where(p => p.UserId == userId))
        {
            if (!byId.TryGetValue(p.FixtureId, out var f)) continue;

            var pts     = GroupMatchScorer.Compute(p.HomeScore, p.AwayScore, f.HomeScore!.Value, f.AwayScore!.Value);
            var isExact = GroupMatchScorer.IsExact(p.HomeScore, p.AwayScore, f.HomeScore!.Value, f.AwayScore!.Value);

            score.GroupMatchPoints += pts;
            if (isExact) score.ExactScorelineCount++;
            if (pts >= 3) score.CorrectOutcomeCount++;
            score.GroupBreakdown.Add(new GroupPredictionScore(p.FixtureId, pts, isExact, pts >= 3));
        }

        return score;
    }

    /// <summary>
    /// Same idea for knockout predictions, using the real streak calculator and scorer.
    /// </summary>
    private static MemberScore ScoreKnockoutUsingProductionScorers(
        string userId,
        IReadOnlyList<KnockoutSlot> slots,
        IReadOnlyList<KnockoutPrediction> preds)
    {
        var completed = slots
            .Where(s => s.Status == MatchStatus.Completed && s.WinnerTeamId != null)
            .ToList();
        var slotByKey = completed.ToDictionary(s => s.SlotKey);

        var predsByUserAndSlot = preds
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, KnockoutPrediction>)g.ToDictionary(p => p.SlotKey));

        var score = new MemberScore { Id = userId, UserId = userId };
        var memo  = new Dictionary<(string, string), int>();

        foreach (var slot in completed.OrderBy(s => (int)s.Round).ThenBy(s => s.SlotNumber))
        {
            if (!predsByUserAndSlot.TryGetValue(userId, out var bySlot)) continue;
            if (!bySlot.TryGetValue(slot.SlotKey, out var pred)) continue;

            var correct = pred.PredictedWinnerTeamId == slot.WinnerTeamId;
            var streakBefore = correct
                ? KnockoutStreakCalculator.FullStreak(userId, slot.SlotKey, slotByKey, predsByUserAndSlot, memo) - 1
                : 0;

            var scorePoints = pred.PredictedHomeScore.HasValue && pred.PredictedAwayScore.HasValue
                              && slot.HomeScore.HasValue && slot.AwayScore.HasValue
                ? GroupMatchScorer.Compute(
                    pred.PredictedHomeScore.Value, pred.PredictedAwayScore.Value,
                    slot.HomeScore.Value, slot.AwayScore.Value)
                : 0;

            var advancing  = correct ? 5 * (streakBefore + 1) : 0;
            var multiplier = correct ? streakBefore + 1 : 0;

            score.KnockoutPoints += scorePoints + advancing;
            score.KnockoutBreakdown.Add(
                new KnockoutPredictionScore(slot.SlotKey, scorePoints, advancing, multiplier));
        }

        return score;
    }

    /// <summary>R32-1 → R16-1 → QF-1, all won by NED.</summary>
    private static KnockoutSlot[] BracketPath() =>
    [
        MakeSlot("R32-1", Round.R32, 1, "NED", "BEL", 2, 0, "NED",
            home: new SlotSource { Type = SlotSourceType.GroupWinner,   Reference = "A" },
            away: new SlotSource { Type = SlotSourceType.GroupRunnerUp, Reference = "B" }),
        MakeSlot("R16-1", Round.R16, 1, "NED", "ESP", 1, 0, "NED",
            home: new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R32-1" },
            away: new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R32-2" }),
        MakeSlot("QF-1", Round.QF, 1, "NED", "BRA", 2, 1, "NED",
            home: new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R16-1" },
            away: new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R16-2" }),
    ];

    private static Fixture MakeFixture(string id, int home, int away) => new()
    {
        Id = id,
        Status = MatchStatus.Completed,
        HomeScore = home,
        AwayScore = away,
    };

    private static GroupPrediction MakeGroupPred(string userId, string fixtureId, int home, int away) => new()
    {
        Id = $"{userId}_{fixtureId}",
        UserId = userId,
        FixtureId = fixtureId,
        HomeScore = home,
        AwayScore = away,
    };

    private static KnockoutSlot MakeSlot(
        string key, Round round, int number,
        string homeTeam, string awayTeam,
        int homeScore, int awayScore, string winner,
        SlotSource home, SlotSource away) => new()
    {
        Id = key,
        SlotKey = key,
        Round = round,
        SlotNumber = number,
        HomeTeamId = homeTeam,
        AwayTeamId = awayTeam,
        HomeScore = homeScore,
        AwayScore = awayScore,
        WinnerTeamId = winner,
        Status = MatchStatus.Completed,
        HomeSlotSource = home,
        AwaySlotSource = away,
    };

    private static KnockoutPrediction MakeKoPred(
        string userId, string slotKey, string winner, int home, int away) => new()
    {
        Id = $"{userId}_{slotKey}",
        UserId = userId,
        SlotKey = slotKey,
        PredictedWinnerTeamId = winner,
        PredictedHomeScore = home,
        PredictedAwayScore = away,
    };

    private static Player MakePlayer(Position position) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Player {position}",
        TeamId = "NED",
        Position = position,
    };

    private static GoalEvent MakeGoal(Guid playerId, GoalType type) => new()
    {
        Id = Guid.NewGuid(),
        FixtureId = "F1",
        PlayerId = playerId,
        Type = type,
        Minute = 10,
    };
}
