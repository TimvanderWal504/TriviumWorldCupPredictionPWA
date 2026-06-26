using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Unit tests for KnockoutMatchScorer.
/// No database required — pure function inputs/outputs.
///
/// Formula: [Group-style score on 90-min result] + [5 × (streakBefore + 1) if advancing team correct]
/// The 90-min score component is never multiplied; only the advancing-team component is.
/// streakBefore = consecutive correct advancing-team predictions immediately before this match.
/// </summary>
public class KnockoutMatchScorerTests
{
    // ── Advancing team only (scores wrong, group component = 0) ──────────────
    // Predicted 1-0, actual 0-1: wrong outcome, wrong GD, no tally match → 0 group pts.

    [Fact]
    public void Streak0_CorrectWinner_WrongScore_Returns5()
    {
        // 0 (group) + 5 × (0+1) = 5
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 0);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void Streak1_CorrectWinner_WrongScore_Returns10()
    {
        // 0 + 5 × (1+1) = 10
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 1);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void Streak2_CorrectWinner_WrongScore_Returns15()
    {
        // 0 + 5 × (2+1) = 15
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 2);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void Streak3_CorrectWinner_WrongScore_Returns20()
    {
        // 0 + 5 × (3+1) = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 3);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void Streak4_CorrectWinner_WrongScore_Returns25()
    {
        // 0 + 5 × (4+1) = 25
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 4);
        Assert.Equal(25, pts);
    }

    [Fact]
    public void Streak10_CorrectWinner_WrongScore_Returns55()
    {
        // 0 + 5 × (10+1) = 55
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 10);
        Assert.Equal(55, pts);
    }

    // ── Exact score + correct winner ──────────────────────────────────────────
    // Group component: 10 (exact). Advancing: 5 × (streakBefore+1).

    [Fact]
    public void Streak0_CorrectWinner_ExactScore_Returns15()
    {
        // 10 + 5 × 1 = 15
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, streakBefore: 0);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void Streak1_CorrectWinner_ExactScore_Returns20()
    {
        // 10 + 5 × 2 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, streakBefore: 1);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void Streak2_CorrectWinner_ExactScore_Returns25()
    {
        // 10 + 5 × 3 = 25
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, streakBefore: 2);
        Assert.Equal(25, pts);
    }

    [Fact]
    public void Streak3_CorrectWinner_ExactScore_Returns30()
    {
        // 10 + 5 × 4 = 30
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, streakBefore: 3);
        Assert.Equal(30, pts);
    }

    [Fact]
    public void Streak4_CorrectWinner_ExactScore_Returns35()
    {
        // 10 + 5 × 5 = 35
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "BRA", 2, 1, streakBefore: 4);
        Assert.Equal(35, pts);
    }

    // ── Exact score but wrong winner ──────────────────────────────────────────
    // Group component: 10. Advancing: 0 (wrong winner). Multiplier does NOT apply.

    [Fact]
    public void ExactScore_WrongWinner_Returns10_RegardlessOfStreak()
    {
        // 10 + 0 = 10 (multiplier does not apply to the score component)
        foreach (var streak in new[] { 0, 1, 2, 5, 10 })
        {
            var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 2, 1, streakBefore: streak);
            Assert.Equal(10, pts);
        }
    }

    [Fact]
    public void ExactScore_WrongWinner_Streak0_Returns10()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 0, 0, "ARG", 0, 0, streakBefore: 0);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void ExactScore_WrongWinner_Streak2_Returns10()
    {
        var pts = KnockoutMatchScorer.Compute("ENG", 1, 0, "FRA", 1, 0, streakBefore: 2);
        Assert.Equal(10, pts);
    }

    // ── Correct goal difference (not exact), wrong winner ────────────────────
    // Group component: 7. Advancing: 0. Total: 7 regardless of streak.

    [Fact]
    public void CorrectGD_WrongWinner_Returns7_RegardlessOfStreak()
    {
        // Predicted 2-1, actual 3-2 — same GD (+1), not exact → 7 group pts.
        foreach (var streak in new[] { 0, 1, 4 })
        {
            var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 3, 2, streakBefore: streak);
            Assert.Equal(7, pts);
        }
    }

    // ── Correct goal difference + correct winner ──────────────────────────────

    [Fact]
    public void Streak4_CorrectGD_CorrectWinner_Returns32()
    {
        // 7 + 5 × 5 = 32
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 3, 2, streakBefore: 4);
        Assert.Equal(32, pts);
    }

    [Fact]
    public void Streak2_CorrectGD_CorrectWinner_Returns22()
    {
        // 7 + 5 × 3 = 22
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 3, 2, streakBefore: 2);
        Assert.Equal(22, pts);
    }

    // ── Correct outcome only (wrong GD), wrong winner ────────────────────────

    [Fact]
    public void CorrectOutcome_NoTally_WrongWinner_Returns3()
    {
        // Predicted 1-0 home win, actual 3-1 home win: same outcome, different GD (+1 vs +2), no tally.
        var pts = KnockoutMatchScorer.Compute("BRA", 1, 0, "ARG", 3, 1, streakBefore: 4);
        Assert.Equal(3, pts);
    }

    // ── Correct outcome + tally bonus, wrong winner ──────────────────────────
    // Predicted 2-0 home win, actual 2-1 home win — correct outcome, tally bonus on home (2==2).

    [Fact]
    public void CorrectOutcomePlusTally_WrongWinner_Returns4()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 0, "ARG", 2, 1, streakBefore: 4);
        Assert.Equal(4, pts);
    }

    // ── Correct outcome + tally bonus + correct winner ────────────────────────

    [Fact]
    public void Streak4_CorrectOutcomePlusTally_CorrectWinner_Returns29()
    {
        // 4 + 5 × 5 = 29
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 0, "ARG", 2, 1, streakBefore: 4);
        Assert.Equal(29, pts);
    }

    // ── Wrong outcome, no tally bonus, wrong winner ───────────────────────────

    [Fact]
    public void WrongOutcome_NoTally_WrongWinner_Returns0()
    {
        // Predicted 1-0 home win, actual 0-1 away win: wrong outcome, wrong GD, no tally.
        foreach (var streak in new[] { 0, 1, 4 })
        {
            var pts = KnockoutMatchScorer.Compute("BRA", 1, 0, "ARG", 0, 1, streakBefore: streak);
            Assert.Equal(0, pts);
        }
    }

    [Fact]
    public void WrongOutcome_NoTally_WrongWinner_Streak0_Returns0()
    {
        // Predicted 2-0 home win, actual 0-3 away win: wrong outcome, wrong GD, no tally.
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 0, "ARG", 0, 3, streakBefore: 0);
        Assert.Equal(0, pts);
    }

    // ── Wrong outcome, tally bonus, wrong winner ──────────────────────────────
    // Predicted 0-1 away win, actual 2-1 home win — wrong outcome.
    // Away predicted 1 == actual 1 → tally bonus. Total: 0 + 1 = 1.

    [Fact]
    public void WrongOutcome_TallyBonus_WrongWinner_Returns1()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 0, 1, "ARG", 2, 1, streakBefore: 4);
        Assert.Equal(1, pts);
    }

    // ── ET/penalties: advancing team is correct, 90-min score differs ─────────

    [Fact]
    public void Streak1_CorrectWinner_AdvancedViaPenalties_WrongPredicted90Min()
    {
        // Team won on penalties; 90-min score was 1-1, user predicted 1-0 → wrong outcome.
        // Predicted home 1 == actual home 1 → tally +1. Wrong outcome: 0 + 1 = 1 group pts.
        // Advancing: 5 × (1+1) = 10. Total: 11.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 0, "ESP", 1, 1, streakBefore: 1);
        Assert.Equal(11, pts);
    }

    [Fact]
    public void Streak2_CorrectWinner_ExactScoreAt90Min_EvenIfMatchWentToET()
    {
        // Predicted 1-1, match also ended 1-1 at 90 min (team then advanced via ET).
        // Group: 10 (exact). Advancing: 5 × (2+1) = 15. Total: 25.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 1, "ESP", 1, 1, streakBefore: 2);
        Assert.Equal(25, pts);
    }

    // ── Null actual scores: result not yet recorded ───────────────────────────

    [Fact]
    public void CorrectWinner_NullActualScores_EarnsAdvancingOnly()
    {
        // Group component cannot be scored → 0. Advancing: 5 × (0+1) = 5.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, null, streakBefore: 0);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void CorrectWinner_NullActualHomeScore_EarnsAdvancingOnly()
    {
        // One actual score null — group component skipped. 5 × (2+1) = 15.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, 1, streakBefore: 2);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void CorrectWinner_NullActualAwayScore_EarnsAdvancingOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, null, streakBefore: 2);
        Assert.Equal(15, pts);
    }

    // ── Null predicted scores: user did not predict a score ──────────────────

    [Fact]
    public void CorrectWinner_NullPredictedScores_EarnsAdvancingOnly()
    {
        // Group component skipped. 5 × (4+1) = 25.
        var pts = KnockoutMatchScorer.Compute("ARG", null, null, "ARG", 2, 1, streakBefore: 4);
        Assert.Equal(25, pts);
    }

    [Fact]
    public void CorrectWinner_NullPredictedHomeScore_EarnsAdvancingOnly()
    {
        // One predicted score null — group component skipped. 5 × (3+1) = 20.
        var pts = KnockoutMatchScorer.Compute("ARG", null, 1, "ARG", 2, 1, streakBefore: 3);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void CorrectWinner_NullPredictedAwayScore_EarnsAdvancingOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, null, "ARG", 2, 1, streakBefore: 3);
        Assert.Equal(20, pts);
    }

    // ── Wrong winner, null scores: still 0 ───────────────────────────────────

    [Fact]
    public void WrongWinner_NullScores_Returns0()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", null, null, "ARG", null, null, streakBefore: 4);
        Assert.Equal(0, pts);
    }

    // ── Score component is never multiplied by streak ─────────────────────────
    // An exact score earns 10 pts regardless of what the streak is.

    [Fact]
    public void ExactScore_WrongWinner_AlwaysReturns10_RegardlessOfStreak()
    {
        foreach (var streak in new[] { 0, 1, 2, 3, 4, 10 })
        {
            var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 2, 1, streakBefore: streak);
            Assert.Equal(10, pts);
        }
    }

    // ── Streak accumulation and reset ────────────────────────────────────────

    [Fact]
    public void AfterWrongPrediction_NextCorrectUsesStreak0()
    {
        // Streak resets to 0 after a miss; next correct earns only 5 × 1 = 5.
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, streakBefore: 0);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void HighStreak_CorrectWinner_EarnsHighBonus()
    {
        // Streak of 15: bonus = 5 × 16 = 80. Score: 10 (exact). Total: 90.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, streakBefore: 15);
        Assert.Equal(90, pts);
    }
}
