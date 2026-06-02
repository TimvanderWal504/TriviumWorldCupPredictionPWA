using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Unit tests for GroupMatchScorer — all tiers and every canonical edge case.
/// No database required; pure function inputs/outputs.
/// TWC-8 acceptance criteria: every tier and edge case, including 2-1 vs 2-2 → 1 pt.
/// </summary>
public class GroupMatchScorerTests
{
    // ── Tier: Exact score (10 pts) ────────────────────────────────────────────

    [Fact]
    public void ExactScore_ReturnstenPoints()
    {
        Assert.Equal(10, GroupMatchScorer.Compute(2, 1, 2, 1));
    }

    [Fact]
    public void ExactScore_Draw_ReturnsTenPoints()
    {
        // 1-1 predicted, 1-1 actual → exact
        Assert.Equal(10, GroupMatchScorer.Compute(1, 1, 1, 1));
    }

    [Fact]
    public void ExactScore_ZeroZero_ReturnsTenPoints()
    {
        Assert.Equal(10, GroupMatchScorer.Compute(0, 0, 0, 0));
    }

    [Fact]
    public void ExactScore_HighScoring_ReturnsTenPoints()
    {
        Assert.Equal(10, GroupMatchScorer.Compute(3, 3, 3, 3));
    }

    // ── Tier: Correct goal difference — not exact (7 pts) ────────────────────

    [Fact]
    public void CorrectGD_NotExact_ReturnsSevenPoints()
    {
        // 3-1 predicted, 4-2 actual: GD = +2 both, not exact → 7
        Assert.Equal(7, GroupMatchScorer.Compute(3, 1, 4, 2));
    }

    [Fact]
    public void CorrectGD_NotExact_DrawGD_ReturnsSevenPoints()
    {
        // 2-2 predicted, 3-3 actual: GD = 0 both, not exact → 7
        Assert.Equal(7, GroupMatchScorer.Compute(2, 2, 3, 3));
    }

    [Fact]
    public void CorrectGD_NotExact_NegativeGD_ReturnsSevenPoints()
    {
        // 0-2 predicted, 1-3 actual: GD = -2 both, not exact → 7
        Assert.Equal(7, GroupMatchScorer.Compute(0, 2, 1, 3));
    }

    [Fact]
    public void CorrectGD_NotExact_NoTallyBonus()
    {
        // 2-0 predicted, 3-1 actual: GD = +2 both (→7), home 2≠3, away 0≠1 → no bonus → 7
        Assert.Equal(7, GroupMatchScorer.Compute(2, 0, 3, 1));
    }

    // ── Tier: Correct outcome only (3 pts) ───────────────────────────────────

    [Fact]
    public void CorrectOutcome_HomeWin_ReturnsThreePoints()
    {
        // 1-0 predicted, 3-1 actual: home win both, different GD (+1 vs +2),
        // home tally 1≠3, away tally 0≠1 → no tally bonus → 3
        Assert.Equal(3, GroupMatchScorer.Compute(1, 0, 3, 1));
    }

    [Fact]
    public void CorrectOutcome_Draw_CannotOccur_BecauseSameGdAlwaysTriggersGdTier()
    {
        // Any draw prediction vs an actual draw always shares GD=0 → scores 7 (GD tier) or 10 (exact).
        // There is no "outcome-only" 3-pt case for draw-vs-draw — the GD tier takes precedence.
        // Verify: 0-0 vs 1-1 → GD=0 both, different → 7 (not 3).
        Assert.Equal(7, GroupMatchScorer.Compute(0, 0, 1, 1));
    }

    [Fact]
    public void CorrectOutcome_HomeWin_ThreePoints_NoGdMatch_NoTallyBonus()
    {
        // 2-0 predicted (home win, GD=+2), 3-2 actual (home win, GD=+1):
        //   GD: +2 ≠ +1 → not GD tier
        //   Outcome: home win both → 3
        //   Home: 2≠3, away: 0≠2 → no tally bonus → 3
        Assert.Equal(3, GroupMatchScorer.Compute(2, 0, 3, 2));
    }

    [Fact]
    public void CorrectOutcome_AwayWin_ReturnsThreePoints()
    {
        // 0-1 predicted, 2-4 actual: away win both, GD -1 vs -2 (different) → 3
        // Home tally: 0≠2, away tally: 1≠4 → no tally bonus → 3
        Assert.Equal(3, GroupMatchScorer.Compute(0, 1, 2, 4));
    }

    // ── Tier: Wrong (0 pts) ───────────────────────────────────────────────────

    [Fact]
    public void WrongOutcome_ReturnsZeroPoints()
    {
        // 1-0 predicted (home win), 0-1 actual (away win) → wrong → 0
        Assert.Equal(0, GroupMatchScorer.Compute(1, 0, 0, 1));
    }

    [Fact]
    public void WrongOutcome_PredictedDrawActualHomeWin_ReturnsOnePoint()
    {
        // 1-1 predicted (draw), 2-1 actual (home win):
        //   Wrong outcome (draw vs home win) → base 0
        //   Home tally: 1 ≠ 2 → no match
        //   Away tally: 1 == 1 → match
        //   Exactly one tally → +1 → total 1
        Assert.Equal(1, GroupMatchScorer.Compute(1, 1, 2, 1));
    }

    [Fact]
    public void WrongOutcome_PredictedDrawActualHomeWin_NoTallyMatch_ReturnsZeroPoints()
    {
        // 2-2 predicted (draw), 3-1 actual (home win):
        //   Wrong outcome (draw vs home win) → base 0
        //   Home tally: 2 ≠ 3 → no match
        //   Away tally: 2 ≠ 1 → no match
        //   No tally bonus → 0
        Assert.Equal(0, GroupMatchScorer.Compute(2, 2, 3, 1));
    }

    [Fact]
    public void WrongOutcome_NoTallyMatch_ReturnsZeroPoints()
    {
        // 3-0 predicted (home win), 0-2 actual (away win), home 3≠0, away 0≠2 → 0
        Assert.Equal(0, GroupMatchScorer.Compute(3, 0, 0, 2));
    }

    // ── Canonical worked example: predicted 2-1, actual 2-2 → 1 pt ──────────

    [Fact]
    public void WorkedExample_Predicted2_1_Actual2_2_ReturnsOnePoint()
    {
        // Predicted 2-1 (home win), actual 2-2 (draw):
        //   Outcome wrong (win vs draw)
        //   GD wrong (+1 vs 0)
        //   Base = 0
        //   Home tally: predicted 2 == actual 2 → match
        //   Away tally: predicted 1 ≠ actual 2 → no match
        //   Exactly one tally correct → +1 bonus
        //   Total = 1
        Assert.Equal(1, GroupMatchScorer.Compute(2, 1, 2, 2));
    }

    // ── Tally bonus: adds to outcome tier (3 → 4) ────────────────────────────

    [Fact]
    public void TallyBonus_AddsToOutcomeTier_AwayTallyCorrect()
    {
        // Predicted 1-0 (home win), actual 2-0 (home win):
        //   Outcome correct → base 3
        //   Home tally: 1 ≠ 2 → no match
        //   Away tally: 0 == 0 → match
        //   Exactly one tally → +1 → total 4
        Assert.Equal(4, GroupMatchScorer.Compute(1, 0, 2, 0));
    }

    [Fact]
    public void TallyBonus_AddsToOutcomeTier_HomeTallyCorrect()
    {
        // Predicted 2-0 (home win), actual 2-1 (home win):
        //   Outcome correct → base 3
        //   Home tally: 2 == 2 → match
        //   Away tally: 0 ≠ 1 → no match
        //   Exactly one tally → +1 → total 4
        Assert.Equal(4, GroupMatchScorer.Compute(2, 0, 2, 1));
    }

    [Fact]
    public void TallyBonus_AddsToWrongTier_WrongWithOneMatch()
    {
        // Predicted 2-1 (home win), actual 2-2 (draw) — the canonical example:
        //   Wrong outcome → base 0
        //   Home tally 2==2 → match; away tally 1≠2 → no match → +1 → total 1
        Assert.Equal(1, GroupMatchScorer.Compute(2, 1, 2, 2));
    }

    [Fact]
    public void TallyBonus_WrongOutcome_AwayTallyMatches_ReturnsOne()
    {
        // Predicted 0-1 (away win), actual 1-1 (draw):
        //   Wrong outcome (away win vs draw) → base 0
        //   Home tally: 0 ≠ 1 → no match
        //   Away tally: 1 == 1 → match
        //   Exactly one tally → +1 → total 1
        Assert.Equal(1, GroupMatchScorer.Compute(0, 1, 1, 1));
    }

    // ── Two correct tallies = exact score (already 10, no further bonus) ─────

    [Fact]
    public void TwoCorrectTallies_IsExact_ReturnsTenNotEleven()
    {
        // If both tallies match, it IS an exact score → 10 pts.
        // The tally bonus path is never reached.
        Assert.Equal(10, GroupMatchScorer.Compute(2, 1, 2, 1));
    }

    // ── Correct GD (not exact): no tally bonus possible ──────────────────────

    [Fact]
    public void CorrectGD_NotExact_NoTallyBonusPossible()
    {
        // 2-0 predicted, 3-1 actual: GD=+2 both.
        // If home matched (2 ≠ 3 doesn't here), mathematical impossibility anyway.
        // home: 2≠3, away: 0≠1 → no tallies match either way.
        // Score = 7 (not 8).
        Assert.Equal(7, GroupMatchScorer.Compute(2, 0, 3, 1));
    }

    [Fact]
    public void CorrectGD_NotExact_OneHomeMatchIsImpossible()
    {
        // 3-0 predicted (GD=+3), 4-1 actual (GD=+3) — home 3≠4, away 0≠1 → 7
        Assert.Equal(7, GroupMatchScorer.Compute(3, 0, 4, 1));
    }

    // ── IsExact helper ────────────────────────────────────────────────────────

    [Fact]
    public void IsExact_SameScores_ReturnsTrue()
    {
        Assert.True(GroupMatchScorer.IsExact(2, 1, 2, 1));
    }

    [Fact]
    public void IsExact_DifferentScores_ReturnsFalse()
    {
        Assert.False(GroupMatchScorer.IsExact(2, 1, 2, 2));
    }

    // ── IsCorrectOutcome helper ───────────────────────────────────────────────

    [Fact]
    public void IsCorrectOutcome_HomeWin_HomeWin_ReturnsTrue()
    {
        Assert.True(GroupMatchScorer.IsCorrectOutcome(1, 0, 3, 1));
    }

    [Fact]
    public void IsCorrectOutcome_HomeWin_Draw_ReturnsFalse()
    {
        Assert.False(GroupMatchScorer.IsCorrectOutcome(1, 0, 1, 1));
    }

    [Fact]
    public void IsCorrectOutcome_HomeWin_AwayWin_ReturnsFalse()
    {
        Assert.False(GroupMatchScorer.IsCorrectOutcome(1, 0, 0, 1));
    }

    [Fact]
    public void IsCorrectOutcome_Draw_Draw_ReturnsTrue()
    {
        Assert.True(GroupMatchScorer.IsCorrectOutcome(0, 0, 2, 2));
    }

    [Fact]
    public void IsCorrectOutcome_AwayWin_AwayWin_ReturnsTrue()
    {
        Assert.True(GroupMatchScorer.IsCorrectOutcome(0, 1, 1, 3));
    }
}
