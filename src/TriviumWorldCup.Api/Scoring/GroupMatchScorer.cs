namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Pure static scoring logic for group-stage match predictions.
/// No database dependency — fully unit-testable with plain values.
///
/// Tier priority (award the single best tier — NOT cumulative):
///   Exact score:                  10 pts
///   Correct goal difference:       7 pts  (same GD, not exact)
///   Correct outcome (W/D/L):       3 pts
///   Wrong:                         0 pts
///
/// Team-tally bonus (+1): when exactly one team's goal count was predicted
/// correctly. Can only add to the outcome tier (3→4) or wrong tier (0→1).
/// Two correct tallies = exact score (already 10 pts). Correct GD that isn't
/// exact makes individual tally matching impossible by arithmetic.
/// </summary>
public static class GroupMatchScorer
{
    /// <summary>
    /// Returns the total points for a single group-stage prediction.
    /// Includes base tier + team-tally bonus where applicable.
    /// </summary>
    public static int Compute(int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        if (IsExact(predictedHome, predictedAway, actualHome, actualAway))
            return 10;

        var predictedGd = predictedHome - predictedAway;
        var actualGd    = actualHome    - actualAway;

        if (predictedGd == actualGd)
        {
            // Correct goal difference but not exact.
            // When GD matches but scores differ, no individual tally can also
            // match: if home tallies matched, away would too (exact), contradiction.
            // So no tally bonus is ever applicable here.
            return 7;
        }

        if (IsCorrectOutcome(predictedHome, predictedAway, actualHome, actualAway))
        {
            // Base = 3. Apply tally bonus only if exactly one team tally matched.
            return 3 + TallyBonus(predictedHome, predictedAway, actualHome, actualAway);
        }

        // Wrong outcome. Apply tally bonus if exactly one team tally matched.
        return 0 + TallyBonus(predictedHome, predictedAway, actualHome, actualAway);
    }

    /// <summary>Returns true if the predicted scoreline exactly matches the actual scoreline.</summary>
    public static bool IsExact(int predictedHome, int predictedAway, int actualHome, int actualAway)
        => predictedHome == actualHome && predictedAway == actualAway;

    /// <summary>
    /// Returns true if the predicted outcome (W/D/L from the home team's perspective)
    /// matches the actual outcome.
    /// </summary>
    public static bool IsCorrectOutcome(int predictedHome, int predictedAway, int actualHome, int actualAway)
        => Math.Sign(predictedHome - predictedAway) == Math.Sign(actualHome - actualAway);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns +1 if exactly one team's goal tally was predicted correctly, 0 otherwise.
    /// Two correct tallies = exact (already handled above).
    /// </summary>
    private static int TallyBonus(int predictedHome, int predictedAway, int actualHome, int actualAway)
    {
        var homeMatch = predictedHome == actualHome;
        var awayMatch = predictedAway == actualAway;

        // Exactly one tally correct → +1 bonus
        return (homeMatch ^ awayMatch) ? 1 : 0;
    }
}
