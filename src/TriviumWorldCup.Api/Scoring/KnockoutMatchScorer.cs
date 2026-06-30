namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Pure static scoring logic for knockout-stage match predictions.
/// No database dependency — fully unit-testable with plain values.
///
/// Total = [Group-style score on 90-min result] + [5 × streak if advancing team correct]
///
/// 90-minute score uses group-stage tiers (no multiplier):
///   Exact score:              10 pts
///   Correct goal difference:   7 pts
///   Correct outcome (W/D/L):   3 pts  (+1 team-tally bonus if applicable)
///   Wrong:                     0 pts  (+1 team-tally bonus if applicable)
///
/// Advancing team bonus (streak-multiplied):
///   Correct advancing team (incl. ET/penalties): 5 × (streakBefore + 1)
///   Wrong advancing team: 0, and streak resets to 0
///
/// streakBefore is the number of consecutive rounds — ending at the match that team won
/// to reach this slot — in which the user correctly predicted that team to advance.
/// R32 is always 0 (no MatchWinner predecessor). See KnockoutStreakCalculator.
/// </summary>
public static class KnockoutMatchScorer
{
    /// <summary>
    /// Computes points for one knockout prediction.
    /// </summary>
    /// <param name="predictedWinnerId">The team the user predicted to advance.</param>
    /// <param name="predictedHomeScore">Predicted 90-minute home score — optional.</param>
    /// <param name="predictedAwayScore">Predicted 90-minute away score — optional.</param>
    /// <param name="actualWinnerId">The team that actually progressed (KnockoutSlot.WinnerTeamId).</param>
    /// <param name="actualHomeScore">Actual 90-minute home score (KnockoutSlot.HomeScore) — null if not yet recorded.</param>
    /// <param name="actualAwayScore">Actual 90-minute away score (KnockoutSlot.AwayScore) — null if not yet recorded.</param>
    /// <param name="streakBefore">
    /// Number of consecutive rounds — ending at the match the team advancing here won
    /// to reach this slot — in which the user correctly predicted that team to advance.
    /// R32 is always 0 (no MatchWinner predecessor). Derived by the caller
    /// (ScoringRecomputeService via KnockoutStreakCalculator) from the bracket feeder chain.
    /// </param>
    /// <returns>Total integer points earned for this prediction.</returns>
    public static int Compute(
        string predictedWinnerId,
        int? predictedHomeScore, int? predictedAwayScore,
        string actualWinnerId,
        int? actualHomeScore, int? actualAwayScore,
        int streakBefore)
    {
        // Group-style scoring on the 90-minute result — not multiplied.
        var scorePoints = 0;
        if (predictedHomeScore.HasValue && predictedAwayScore.HasValue
            && actualHomeScore.HasValue && actualAwayScore.HasValue)
        {
            scorePoints = GroupMatchScorer.Compute(
                predictedHomeScore.Value, predictedAwayScore.Value,
                actualHomeScore.Value, actualAwayScore.Value);
        }

        // Advancing team: 5 × (streakBefore + 1) — independent of the 90-min score.
        var advancingPoints = predictedWinnerId == actualWinnerId
            ? 5 * (streakBefore + 1)
            : 0;

        return scorePoints + advancingPoints;
    }
}
