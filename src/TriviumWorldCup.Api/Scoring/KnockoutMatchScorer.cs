using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Pure static scoring logic for knockout-stage match predictions.
/// No database dependency — fully unit-testable with plain values.
///
/// Per-match scoring (before round multiplier):
///   Correct advancing team (incl. ET/penalties): 5 pts
///   Exact 90-minute score bonus (only when winner is also correct): +3 pts
///
/// Round multipliers:
///   R32         × 1.0
///   R16         × 1.5
///   QF          × 2.0
///   SF          × 2.5
///   ThirdPlace  × 2.5
///   Final       × 3.0
/// </summary>
public static class KnockoutMatchScorer
{
    /// <summary>Returns the round multiplier for the given round.</summary>
    public static double Multiplier(Round round) => round switch
    {
        Round.R32        => 1.0,
        Round.R16        => 1.5,
        Round.QF         => 2.0,
        Round.SF         => 2.5,
        Round.ThirdPlace => 2.5,
        Round.Final      => 3.0,
        _                => throw new ArgumentOutOfRangeException(nameof(round))
    };

    /// <summary>
    /// Computes points for one knockout prediction.
    /// </summary>
    /// <param name="predictedWinnerId">The team the user predicted to advance.</param>
    /// <param name="predictedHomeScore">Predicted 90-minute home score — optional.</param>
    /// <param name="predictedAwayScore">Predicted 90-minute away score — optional.</param>
    /// <param name="actualWinnerId">The team that actually progressed (KnockoutSlot.WinnerTeamId).</param>
    /// <param name="actualHomeScore">Actual 90-minute home score (KnockoutSlot.HomeScore) — null if not yet recorded.</param>
    /// <param name="actualAwayScore">Actual 90-minute away score (KnockoutSlot.AwayScore) — null if not yet recorded.</param>
    /// <param name="round">The knockout round — determines the multiplier.</param>
    /// <returns>Total integer points earned for this prediction.</returns>
    public static int Compute(
        string predictedWinnerId,
        int? predictedHomeScore, int? predictedAwayScore,
        string actualWinnerId,
        int? actualHomeScore, int? actualAwayScore,
        Round round)
    {
        var basePoints = 0;

        if (predictedWinnerId == actualWinnerId)
            basePoints += 5;

        // Score bonus only applies when the winner was also correct.
        if (basePoints > 0
            && predictedHomeScore.HasValue && predictedAwayScore.HasValue
            && actualHomeScore.HasValue && actualAwayScore.HasValue
            && predictedHomeScore.Value == actualHomeScore.Value
            && predictedAwayScore.Value == actualAwayScore.Value)
            basePoints += 3;

        return (int)(basePoints * Multiplier(round));
    }
}
