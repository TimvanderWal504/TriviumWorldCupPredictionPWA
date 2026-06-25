using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Pure static scoring logic for knockout-stage match predictions.
/// No database dependency — fully unit-testable with plain values.
///
/// Total = [Group-style score on 90-min result] + [5 × Multiplier(round) if advancing team correct]
///
/// 90-minute score uses group-stage tiers (no multiplier):
///   Exact score:              10 pts
///   Correct goal difference:   7 pts
///   Correct outcome (W/D/L):   3 pts  (+1 team-tally bonus if applicable)
///   Wrong:                     0 pts  (+1 team-tally bonus if applicable)
///
/// Advancing team bonus (multiplied by round):
///   Correct advancing team (incl. ET/penalties): 5 × round multiplier
///
/// Round multipliers:
///   R32         × 1.0
///   R16         × 2.0
///   QF          × 3.0
///   SF          × 4.0
///   ThirdPlace  × 4.0
///   Final       × 5.0
/// </summary>
public static class KnockoutMatchScorer
{
    /// <summary>Returns the round multiplier for the given round.</summary>
    public static double Multiplier(Round round) => round switch
    {
        Round.R32        => 1.0,
        Round.R16        => 2.0,
        Round.QF         => 3.0,
        Round.SF         => 4.0,
        Round.ThirdPlace => 4.0,
        Round.Final      => 5.0,
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
    /// <param name="round">The knockout round — determines the advancing-team multiplier.</param>
    /// <returns>Total integer points earned for this prediction.</returns>
    public static int Compute(
        string predictedWinnerId,
        int? predictedHomeScore, int? predictedAwayScore,
        string actualWinnerId,
        int? actualHomeScore, int? actualAwayScore,
        Round round)
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

        // Advancing team: 5 × round multiplier — independent of the 90-min score.
        var advancingPoints = predictedWinnerId == actualWinnerId
            ? (int)(5 * Multiplier(round))
            : 0;

        return scorePoints + advancingPoints;
    }
}
