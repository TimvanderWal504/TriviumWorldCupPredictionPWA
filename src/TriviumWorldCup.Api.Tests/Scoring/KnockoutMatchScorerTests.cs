using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Unit tests for KnockoutMatchScorer.
/// No database required — pure function inputs/outputs.
/// TWC-15 acceptance criteria: each round multiplier, advance-vs-score independence,
/// SF/3rd-place both at ×2.5, canonical Final example = 24 pts.
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
    public void Multiplier_R16_ReturnsOnePointFive()
    {
        Assert.Equal(1.5, KnockoutMatchScorer.Multiplier(Round.R16));
    }

    [Fact]
    public void Multiplier_QF_ReturnsTwo()
    {
        Assert.Equal(2.0, KnockoutMatchScorer.Multiplier(Round.QF));
    }

    [Fact]
    public void Multiplier_SF_ReturnsTwoPointFive()
    {
        Assert.Equal(2.5, KnockoutMatchScorer.Multiplier(Round.SF));
    }

    [Fact]
    public void Multiplier_ThirdPlace_ReturnsTwoPointFive()
    {
        Assert.Equal(2.5, KnockoutMatchScorer.Multiplier(Round.ThirdPlace));
    }

    [Fact]
    public void Multiplier_Final_ReturnsThree()
    {
        Assert.Equal(3.0, KnockoutMatchScorer.Multiplier(Round.Final));
    }

    [Fact]
    public void Multiplier_InvalidRound_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KnockoutMatchScorer.Multiplier((Round)99));
    }

    // ── SF and ThirdPlace both at ×2.5 (explicit per acceptance criteria) ─────

    [Fact]
    public void SF_And_ThirdPlace_HaveSameMultiplier()
    {
        Assert.Equal(KnockoutMatchScorer.Multiplier(Round.SF),
                     KnockoutMatchScorer.Multiplier(Round.ThirdPlace));
    }

    // ── Canonical Final example: correct winner + exact 90-min score = 24 ─────

    [Fact]
    public void Final_CorrectWinner_ExactScore_Returns24()
    {
        // (5 + 3) × 3.0 = 24
        var pts = KnockoutMatchScorer.Compute(
            predictedWinnerId:  "BRA",
            predictedHomeScore: 2, predictedAwayScore: 1,
            actualWinnerId:     "BRA",
            actualHomeScore:    2, actualAwayScore: 1,
            round: Round.Final);

        Assert.Equal(24, pts);
    }

    // ── Correct winner only (no exact score) — 5 × multiplier per round ──────

    [Fact]
    public void R32_CorrectWinner_WrongScore_Returns5()
    {
        // 5 × 1.0 = 5
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.R32);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void R16_CorrectWinner_WrongScore_Returns7()
    {
        // 5 × 1.5 = 7 (int truncation of 7.5 — but 5×1.5=7.5→7; note: 5*1.5=7.5 truncates to 7)
        // Actually 5 × 1.5 = 7.5 → (int) = 7
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.R16);
        Assert.Equal(7, pts);
    }

    [Fact]
    public void QF_CorrectWinner_WrongScore_Returns10()
    {
        // 5 × 2.0 = 10
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.QF);
        Assert.Equal(10, pts);
    }

    [Fact]
    public void SF_CorrectWinner_WrongScore_Returns12()
    {
        // 5 × 2.5 = 12 (int truncation of 12.5 → 12)
        // Actually 5 × 2.5 = 12.5 → (int) = 12
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.SF);
        Assert.Equal(12, pts);
    }

    [Fact]
    public void ThirdPlace_CorrectWinner_WrongScore_Returns12()
    {
        // 5 × 2.5 = 12.5 → (int) = 12
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.ThirdPlace);
        Assert.Equal(12, pts);
    }

    [Fact]
    public void Final_CorrectWinner_WrongScore_Returns15()
    {
        // 5 × 3.0 = 15
        var pts = KnockoutMatchScorer.Compute("ARG", 1, 0, "ARG", 2, 1, Round.Final);
        Assert.Equal(15, pts);
    }

    // ── Correct winner + exact score — (5+3) × multiplier per round ──────────

    [Fact]
    public void R32_CorrectWinner_ExactScore_Returns8()
    {
        // (5 + 3) × 1.0 = 8
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.R32);
        Assert.Equal(8, pts);
    }

    [Fact]
    public void R16_CorrectWinner_ExactScore_Returns12()
    {
        // (5 + 3) × 1.5 = 12
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.R16);
        Assert.Equal(12, pts);
    }

    [Fact]
    public void QF_CorrectWinner_ExactScore_Returns16()
    {
        // (5 + 3) × 2.0 = 16
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.QF);
        Assert.Equal(16, pts);
    }

    [Fact]
    public void SF_CorrectWinner_ExactScore_Returns20()
    {
        // (5 + 3) × 2.5 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.SF);
        Assert.Equal(20, pts);
    }

    [Fact]
    public void ThirdPlace_CorrectWinner_ExactScore_Returns20()
    {
        // (5 + 3) × 2.5 = 20
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, 1, Round.ThirdPlace);
        Assert.Equal(20, pts);
    }

    // ── Wrong winner + wrong score: 0 ────────────────────────────────────────

    [Fact]
    public void WrongWinner_WrongScore_Returns0_Final()
    {
        // Wrong winner AND wrong score — no points at all.
        var pts = KnockoutMatchScorer.Compute("BRA", 3, 1, "ARG", 2, 1, Round.Final);
        Assert.Equal(0, pts);
    }

    [Fact]
    public void WrongWinner_WrongScore_Returns0_QF()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 3, 0, "ARG", 2, 1, Round.QF);
        Assert.Equal(0, pts);
    }

    [Fact]
    public void WrongWinner_WrongScore_Returns0_R32()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", 1, 0, "ARG", 0, 0, Round.R32);
        Assert.Equal(0, pts);
    }

    // ── Exact score but wrong winner: score bonus only (3 × multiplier) ─────────
    // The score bonus is independent — earned even when the advancing team is wrong.

    [Fact]
    public void ExactScore_WrongWinner_EarnsScoreBonusOnly_Final()
    {
        // Predicted BRA wins 2-1; actual ARG wins 2-1 — score exact, winner wrong.
        // 0 (wrong winner) + 3 (exact score) = 3 × 3.0 = 9
        var pts = KnockoutMatchScorer.Compute("BRA", 2, 1, "ARG", 2, 1, Round.Final);
        Assert.Equal(9, pts);
    }

    [Fact]
    public void ExactScore_WrongWinner_EarnsScoreBonusOnly_SF()
    {
        // 0 + 3 = 3 × 2.5 = 7 (integer truncation of 7.5)
        var pts = KnockoutMatchScorer.Compute("ENG", 1, 0, "FRA", 1, 0, Round.SF);
        Assert.Equal(7, pts);
    }

    [Fact]
    public void ExactScore_WrongWinner_EarnsScoreBonusOnly_R32()
    {
        // 0 + 3 = 3 × 1.0 = 3
        var pts = KnockoutMatchScorer.Compute("BRA", 0, 0, "ARG", 0, 0, Round.R32);
        Assert.Equal(3, pts);
    }

    // ── Advance-vs-score independence ─────────────────────────────────────────

    [Fact]
    public void CorrectWinner_AdvancedViaPenalties_NoExactScore_EarnsAdvancementPointsOnly()
    {
        // Team won on penalties; 90-min score was 1-1 (draw), user predicted 1-0.
        // Winner correct → 5 pts. 90-min score wrong → no bonus. R16: 5 × 1.5 = 7.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 0, "ESP", 1, 1, Round.R16);
        Assert.Equal(7, pts);
    }

    [Fact]
    public void CorrectWinner_ExactScoreAfter90Min_EarnsFullPoints_EvenIfMatchWentToET()
    {
        // Team advanced via ET; the 90-min score stored was 1-1 which the user predicted.
        // Winner correct: +5. Exact 90-min score: +3. QF: × 2.0 = 16.
        var pts = KnockoutMatchScorer.Compute("ESP", 1, 1, "ESP", 1, 1, Round.QF);
        Assert.Equal(16, pts);
    }

    // ── Null scores: actual scores not yet available ──────────────────────────

    [Fact]
    public void CorrectWinner_NullActualScores_EarnsAdvancementOnly_NoBonus()
    {
        // actualHomeScore and actualAwayScore are null (result not yet recorded in full).
        // Winner correct → 5 pts. Bonus not possible. R32: 5 × 1.0 = 5.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, null, Round.R32);
        Assert.Equal(5, pts);
    }

    [Fact]
    public void CorrectWinner_NullActualHomeScore_EarnsAdvancementOnly()
    {
        // Only one actual score available — bonus requires both.
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", null, 1, Round.QF);
        Assert.Equal(10, pts); // 5 × 2.0 = 10
    }

    [Fact]
    public void CorrectWinner_NullActualAwayScore_EarnsAdvancementOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, 1, "ARG", 2, null, Round.QF);
        Assert.Equal(10, pts); // 5 × 2.0 = 10
    }

    // ── Null predicted scores: user did not predict a score ──────────────────

    [Fact]
    public void CorrectWinner_NullPredictedScores_EarnsAdvancementOnly()
    {
        // User predicted winner but gave no score.
        // Winner correct → 5 pts. Bonus not possible. Final: 5 × 3.0 = 15.
        var pts = KnockoutMatchScorer.Compute("ARG", null, null, "ARG", 2, 1, Round.Final);
        Assert.Equal(15, pts);
    }

    [Fact]
    public void CorrectWinner_NullPredictedHomeScore_EarnsAdvancementOnly()
    {
        // Only one predicted score available — bonus requires both.
        var pts = KnockoutMatchScorer.Compute("ARG", null, 1, "ARG", 2, 1, Round.SF);
        Assert.Equal(12, pts); // 5 × 2.5 = 12
    }

    [Fact]
    public void CorrectWinner_NullPredictedAwayScore_EarnsAdvancementOnly()
    {
        var pts = KnockoutMatchScorer.Compute("ARG", 2, null, "ARG", 2, 1, Round.SF);
        Assert.Equal(12, pts); // 5 × 2.5 = 12
    }

    // ── Wrong winner with null scores: still 0 ────────────────────────────────

    [Fact]
    public void WrongWinner_NullScores_Returns0()
    {
        var pts = KnockoutMatchScorer.Compute("BRA", null, null, "ARG", null, null, Round.Final);
        Assert.Equal(0, pts);
    }

    // ── SF and ThirdPlace explicit points parity ──────────────────────────────

    [Fact]
    public void SF_CorrectWinnerExactScore_EqualToThirdPlace()
    {
        var sfPts = KnockoutMatchScorer.Compute("FRA", 1, 0, "FRA", 1, 0, Round.SF);
        var tpPts = KnockoutMatchScorer.Compute("FRA", 1, 0, "FRA", 1, 0, Round.ThirdPlace);
        Assert.Equal(sfPts, tpPts);
        Assert.Equal(20, sfPts); // (5+3) × 2.5 = 20
    }

    [Fact]
    public void SF_WrongWinner_EqualToThirdPlace_WrongWinner()
    {
        // Wrong winner + wrong score: both rounds give 0, confirming SF == ThirdPlace.
        var sfPts = KnockoutMatchScorer.Compute("FRA", 2, 0, "ESP", 1, 0, Round.SF);
        var tpPts = KnockoutMatchScorer.Compute("FRA", 2, 0, "ESP", 1, 0, Round.ThirdPlace);
        Assert.Equal(0, sfPts);
        Assert.Equal(0, tpPts);
    }
}
