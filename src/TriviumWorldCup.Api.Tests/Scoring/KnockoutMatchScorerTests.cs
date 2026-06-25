using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Unit tests for KnockoutMatchScorer.
/// No database required — pure function inputs/outputs.
///
/// Formula: [Group-style score on 90-min result] + [5 × Multiplier if advancing team correct]
/// The 90-min score component is never multiplied; only the advancing-team component is.
/// </summary>
public class KnockoutMatchScorerTests
{
    // ── Multiplier: one test per Round value ──────────────────────────────────

    [Fact]
    public void Multiplier_R32_ReturnsOne()
    {
        Assert.Equal(1.0, KnockoutMatchScorer.Multiplier(Round.R32));
    }

    [Fact]
    public void Multiplier_R16_ReturnsTwo()
    {
        Assert.Equal(2.0, KnockoutMatchScorer.Multiplier(Round.R16));
    }

    [Fact]
    public void Multiplier_QF_ReturnsThree()
    {
        Assert.Equal(3.0, KnockoutMatchScorer.Multiplier(Round.QF));
    }

    [Fact]
    public void Multiplier_SF_ReturnsFour()
    {
        Assert.Equal(4.0, KnockoutMatchScorer.Multiplier(Round.SF));
    }

    [Fact]
    public void Multiplier_ThirdPlace_ReturnsFour()
    {
        Assert.Equal(4.0, KnockoutMatchScorer.Multiplier(Round.ThirdPlace));
    }

    [Fact]
    public void Multiplier_Final_ReturnsFive()
    {
        Assert.Equal(5.0, KnockoutMatchScorer.Multiplier(Round.Final));
    }

    [Fact]
    public void Multiplier_InvalidRound_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KnockoutMatchScorer.Multiplier((Round)99));
    }

    [Fact]
    public void SF_And_ThirdPlace_HaveSameMultiplier()
    {
        Assert.Equal(KnockoutMatchScorer.Multiplier(Round.SF),
                     KnockoutMatchScorer.Multiplier(Round.ThirdPlace));
    }

    // ── Advancing team only (scores wrong, group component = 0) ──────────────
    // Predicted 1-0, actual 0-1: wrong outcome, wrong GD, no tally match → 0 group pts.

    [Fact]
    public void R32_CorrectWinner_WrongScore_Returns5()
    {
        // 0 (group) + 5 × 1.0 = 5
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.R32);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void R16_CorrectWinner_WrongScore_Returns10()
    {
        // 0 + 5 × 2.0 = 10
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.R16);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void QF_CorrectWinner_WrongScore_Returns15()
    {
        // 0 + 5 × 3.0 = 15
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.QF);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void SF_CorrectWinner_WrongScore_Returns20()
    {
        // 0 + 5 × 4.0 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.SF);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void ThirdPlace_CorrectWinner_WrongScore_Returns20()
    {
        // 0 + 5 × 4.0 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.ThirdPlace);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void Final_CorrectWinner_WrongScore_Returns25()
    {
        // 0 + 5 × 5.0 = 25
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 0, 1, Round.Final);
        Assert.Equal(25, pts);
    }

    // ── Exact score + correct winner ──────────────────────────────────────────
    // Group component: 10 (exact). Advancing: 5 × multiplier.

    [Fact]
    public void R32_CorrectWinner_ExactScore_Returns15()
    {
        // 10 + 5 × 1.0 = 15
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.R32);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void R16_CorrectWinner_ExactScore_Returns20()
    {
        // 10 + 5 × 2.0 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.R16);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void QF_CorrectWinner_ExactScore_Returns25()
    {
        // 10 + 5 × 3.0 = 25
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.QF);
        Assert.Equal(25, pts);
    }

    [Fact]
    public void SF_CorrectWinner_ExactScore_Returns30()
    {
        // 10 + 5 × 4.0 = 30
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.SF);
        Assert.Equal(30, pts);
    }

    [Fact]
    public void ThirdPlace_CorrectWinner_ExactScore_Returns30()
    {
        // 10 + 5 × 4.0 = 30
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.ThirdPlace);
        Assert.Equal(30, pts);
    }

    [Fact]
    public void Final_CorrectWinner_ExactScore_Returns35()
    {
        // 10 + 5 × 5.0 = 35 — canonical Final example
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "BRA", 2, 1, Round.Final);
        Assert.Equal(35, pts);
    }

    // ── Exact score but wrong winner ──────────────────────────────────────────
    // Group component: 10. Advancing: 0 (wrong winner). Multiplier does NOT apply.

    [Fact]
    public void Final_ExactScore_WrongWinner_Returns10()
    {
        // 10 + 0 = 10 (multiplier does not apply to the score component)
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 2, 1, Round.Final);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void R32_ExactScore_WrongWinner_Returns10()
    {
        // 10 + 0 = 10 (same regardless of round — score is not multiplied)
        var pts = KnockoutMatchScorer.Compute("BRA", 0, 0, "ARG", 0, 0, Round.R32);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void QF_ExactScore_WrongWinner_Returns10()
    {
        var pts = KnockoutMatchScorer.Compute("ENG", 1, 0, "FRA", 1, 0, Round.QF);
        Assert.Equal(10, pts);
    }

    // ── Correct goal difference (not exact), wrong winner ────────────────────
    // Group component: 7. Advancing: 0. Total: 7 regardless of round.

    [Fact]
    public void Final_CorrectGD_WrongWinner_Returns7()
    {
        // Predicted 2-1, actual 3-2 — same GD (+1), not exact → 7 group pts.
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 3, 2, Round.Final);
        Assert.Equal(7, pts);
    }

    [Fact]
    public void R32_CorrectGD_WrongWinner_Returns7()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 3, 2, Round.R32);
        Assert.Equal(7, pts);
    }

    // ── Correct goal difference + correct winner ──────────────────────────────

    [Fact]
    public void Final_CorrectGD_CorrectWinner_Returns32()
    {
        // 7 + 5 × 5.0 = 32
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 3, 2, Round.Final);
        Assert.Equal(32, pts);
    }

    [Fact]
    public void QF_CorrectGD_CorrectWinner_Returns22()
    {
        // 7 + 5 × 3.0 = 22
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 3, 2, Round.QF);
        Assert.Equal(22, pts);
    }

    // ── Correct outcome only (wrong GD), wrong winner ────────────────────────
    // Predicted 2-0 home win, actual 3-1 home win — correct outcome, different GD.
    // No tally bonus: home predicted 2 vs actual 3, away predicted 0 vs actual 1 — neither matches.

    [Fact]
    public void Final_CorrectOutcome_NoTally_WrongWinner_Returns3()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 0, "ARG", 3, 1, Round.Final);
        Assert.Equal(3, pts);
    }

    // ── Correct outcome + tally bonus, wrong winner ──────────────────────────
    // Predicted 2-0 home win, actual 2-1 home win — correct outcome, tally bonus on home (2==2).
    // GD: predicted +2, actual +1 — different. Outcome: both home wins.
    // Tally: home predicted 2 == actual 2 ✓; away predicted 0 ≠ actual 1 ✗ → exactly one → +1.

    [Fact]
    public void Final_CorrectOutcomePlusTally_WrongWinner_Returns4()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 0, "ARG", 2, 1, Round.Final);
        Assert.Equal(4, pts);
    }

    // ── Correct outcome + tally bonus + correct winner ────────────────────────

    [Fact]
    public void Final_CorrectOutcomePlusTally_CorrectWinner_Returns29()
    {
        // 4 + 5 × 5.0 = 29
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 0, "ARG", 2, 1, Round.Final);
        Assert.Equal(29, pts);
    }

    // ── Wrong outcome, tally bonus only ──────────────────────────────────────
    // Predicted 1-0 home win, actual 0-1 away win — wrong outcome.
    // Tally: home 1≠0, away 0≠1 → no tally. Total: 0.

    [Fact]
    public void WrongOutcome_NoTally_WrongWinner_Returns0_Final()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 3, 1, "ARG", 2, 1, Round.Final);
        Assert.Equal(0, pts);
    }

    [Fact]
    public void WrongOutcome_NoTally_WrongWinner_Returns0_QF()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 3, 0, "ARG", 2, 1, Round.QF);
        Assert.Equal(0, pts);
    }

    [Fact]
    public void WrongOutcome_NoTally_WrongWinner_Returns0_R32()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 1, 0, "ARG", 0, 0, Round.R32);
        Assert.Equal(0, pts);
    }

    // ── Wrong outcome, tally bonus, wrong winner ──────────────────────────────
    // Predicted 2-1 home win, actual 1-2 away win — wrong outcome.
    // Tally: home 2≠1, away 1≠2? No. home predicted 2, actual 1 (away score)...
    // Wait, these are team tallies: home predicted 2 vs actual home 1, away predicted 1 vs actual away 2.
    // No match on either → 0 tally. Let me use a case with one tally:
    // Predicted 1-0 home win, actual 2-1 away win (wrong outcome: predicted home, actual away).
    // Tally: home 1≠2, away 0≠1 — no match.
    // Better: predicted 2-1 home win (home 2, away 1), actual 1-2 away win (home 1, away 2).
    // Tally: home 2≠1, away 1≠2 — no match → 0.
    // Try: predicted 0-1 away win, actual 2-1 home win.
    // Tally: home 0≠2, away 1==1 ✓ — exactly one → +1. Outcome wrong. Total: 0+1=1.

    [Fact]
    public void WrongOutcome_TallyBonus_WrongWinner_Returns1_Final()
    {
        // Predicted 0-1 (away win), actual 2-1 (home win) — wrong outcome.
        // Away predicted 1 == actual 1 → tally bonus. Total: 0 + 1 = 1.
        var pts = KnockoutMatchScorer.Compute("BRA", 0, 1, "ARG", 2, 1, Round.Final);
        Assert.Equal(1, pts);
    }

    // ── ET/penalties: advancing team is correct, 90-min score differs ─────────

    [Fact]
    public void CorrectWinner_AdvancedViaPenalties_WrongPredicted90Min_EarnsAdvancingOnly()
    {
        // Team won on penalties; 90-min score was 1-1, user predicted 1-0 → wrong outcome.
        // Group: 0 (wrong outcome, no tally: home 1≠1? Wait, 1==1 home tally... hmm.
        // Predicted home 1, actual home 1 ✓. Predicted away 0, actual away 1 ✗. → tally +1.
        // Wrong outcome: predicted home win, actual draw → 0 + 1 = 1 group pts.
        // Advancing: 5 × 2.0 = 10. Total: 11.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 0, "ESP", 1, 1, Round.R16);
        Assert.Equal(11, pts);
    }

    [Fact]
    public void CorrectWinner_ExactScoreAt90Min_EarnsFullPoints_EvenIfMatchWentToET()
    {
        // Predicted 1-1, match also ended 1-1 at 90 min (team then advanced via ET).
        // Group: 10 (exact). Advancing: 5 × 3.0 = 15. Total: 25.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 1, "ESP", 1, 1, Round.QF);
        Assert.Equal(25, pts);
    }

    // ── Null actual scores: result not yet recorded ───────────────────────────

    [Fact]
    public void CorrectWinner_NullActualScores_EarnsAdvancingOnly()
    {
        // Group component cannot be scored → 0. Advancing: 5 × 1.0 = 5.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, null, Round.R32);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void CorrectWinner_NullActualHomeScore_EarnsAdvancingOnly()
    {
        // One actual score null — group component skipped. 5 × 3.0 = 15.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, 1, Round.QF);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void CorrectWinner_NullActualAwayScore_EarnsAdvancingOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, null, Round.QF);
        Assert.Equal(15, pts);
    }

    // ── Null predicted scores: user did not predict a score ──────────────────

    [Fact]
    public void CorrectWinner_NullPredictedScores_EarnsAdvancingOnly()
    {
        // Group component skipped. Final: 5 × 5.0 = 25.
        var pts = KnockoutMatchScorer.Compute("ARG", null, null, "ARG", 2, 1, Round.Final);
        Assert.Equal(25, pts);
    }

    [Fact]
    public void CorrectWinner_NullPredictedHomeScore_EarnsAdvancingOnly()
    {
        // One predicted score null — group component skipped. SF: 5 × 4.0 = 20.
        var pts = KnockoutMatchScorer.Compute("ARG", null, 1, "ARG", 2, 1, Round.SF);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void CorrectWinner_NullPredictedAwayScore_EarnsAdvancingOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, null, "ARG", 2, 1, Round.SF);
        Assert.Equal(20, pts);
    }

    // ── Wrong winner, null scores: still 0 ───────────────────────────────────

    [Fact]
    public void WrongWinner_NullScores_Returns0()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", null, null, "ARG", null, null, Round.Final);
        Assert.Equal(0, pts);
    }

    // ── SF and ThirdPlace explicit parity ────────────────────────────────────

    [Fact]
    public void SF_And_ThirdPlace_ExactScore_EqualPoints()
    {
        var sfPts = KnockoutMatchScorer.Compute("FRA", 1, 0, "FRA", 1, 0, Round.SF);
        var tpPts = KnockoutMatchScorer.Compute("FRA", 1, 0, "FRA", 1, 0, Round.ThirdPlace);
        Assert.Equal(sfPts, tpPts);
        Assert.Equal(30, sfPts); // 10 + 5 × 4.0 = 30
    }

    [Fact]
    public void SF_And_ThirdPlace_WrongWinner_EqualPoints()
    {
        var sfPts = KnockoutMatchScorer.Compute("FRA", 2, 0, "ESP", 1, 0, Round.SF);
        var tpPts = KnockoutMatchScorer.Compute("FRA", 2, 0, "ESP", 1, 0, Round.ThirdPlace);
        Assert.Equal(sfPts, tpPts);
    }

    // ── Score component is never multiplied by round ──────────────────────────
    // An exact score earns 10 pts regardless of which round it's in.

    [Fact]
    public void ExactScore_WrongWinner_AlwaysReturns10_RegardlessOfRound()
    {
        foreach (var round in new[] { Round.R32, Round.R16, Round.QF, Round.SF, Round.ThirdPlace, Round.Final })
        {
            var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 2, 1, round);
            Assert.Equal(10, pts);
        }
    }
}
