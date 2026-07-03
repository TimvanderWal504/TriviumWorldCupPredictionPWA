using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// TWC-63: identifies knockout predictions that reference a team no longer participating in
/// their slot after a bracket rewiring (TWC-62 migration) or an admin team override
/// (POST /admin/knockout/{slotKey}/teams). Such predictions are stale — scoring would
/// silently award 0 and reset the user's streak, and the UI would show a predicted winner
/// against a matchup that no longer exists.
///
/// Judgment call (documented in TWC-63): stale predictions are deleted rather than flagged.
/// KnockoutPrediction carries no existing "invalidated" field, and every consumer
/// (ScoringRecomputeService, KnockoutStreakCalculator, leaderboard drill-down, results page)
/// already treats "no KnockoutPrediction document for this user+slot" as "no pick" — so
/// deletion requires no downstream changes, whereas a flag would require updating every
/// consumer to check it. Users may re-predict on the slot afterward as long as it remains
/// unlocked (the participant change does not itself lock the slot).
/// Pure function — no database dependency.
/// </summary>
public static class KnockoutPredictionInvalidator
{
    /// <summary>
    /// Returns the subset of <paramref name="predictions"/> (expected to all share
    /// <paramref name="slot"/>'s SlotKey) whose PredictedWinnerTeamId is no longer one of the
    /// slot's current participants (HomeTeamId / AwayTeamId), compared case-insensitively.
    /// If either team is now null (bracket not yet resolved), all predictions are considered stale.
    /// </summary>
    public static IReadOnlyList<KnockoutPrediction> FindStale(
        KnockoutSlot slot,
        IEnumerable<KnockoutPrediction> predictions)
    {
        return predictions
            .Where(p => !IsStillParticipant(p.PredictedWinnerTeamId, slot))
            .ToList();
    }

    private static bool IsStillParticipant(string predictedWinnerTeamId, KnockoutSlot slot) =>
        string.Equals(predictedWinnerTeamId, slot.HomeTeamId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(predictedWinnerTeamId, slot.AwayTeamId, StringComparison.OrdinalIgnoreCase);
}
