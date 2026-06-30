using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Computes the per-team streak before a given knockout slot for a user.
///
/// The streak follows a team along its bracket path (e.g. R32 → R16 → QF → SF → Final),
/// not a global run of correct picks. R32 always starts fresh: the feeder is a group
/// placement, not a preceding knockout match. The third-place play-off can never extend a
/// winning-team streak because its feeder is a MatchLoser source.
/// </summary>
public static class KnockoutStreakCalculator
{
    /// <summary>
    /// Returns the number of consecutive rounds — ending at the match the advancing team won
    /// to reach <paramref name="slotKey"/> — in which <paramref name="userId"/> correctly
    /// predicted that team to advance. Returns 0 when the pick in this slot was wrong/absent,
    /// or when the slot is R32 / third-place (no MatchWinner feeder exists).
    ///
    /// Pass this value directly as <c>streakBefore</c> to <see cref="KnockoutMatchScorer.Compute"/>.
    /// </summary>
    public static int StreakBefore(
        string userId,
        string slotKey,
        IReadOnlyDictionary<string, KnockoutSlot> slotByKey,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, KnockoutPrediction>> predsByUserAndSlot)
    {
        var memo = new Dictionary<(string, string), int>();
        var fullStreak = FullStreak(userId, slotKey, slotByKey, predsByUserAndSlot, memo);

        // StreakBefore = full streak at this slot minus 1 (the current slot itself).
        // If the user got the current slot wrong, FullStreak returns 0 and we return 0.
        return fullStreak > 0 ? fullStreak - 1 : 0;
    }

    // Returns 0 when the user did NOT predict the actual winner correctly in this slot.
    // Returns 1 + feeder's FullStreak when they did, and the feeder exists.
    // Returns 1 when they did and this is an R32 or third-place slot (no MatchWinner feeder).
    internal static int FullStreak(
        string userId,
        string slotKey,
        IReadOnlyDictionary<string, KnockoutSlot> slotByKey,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, KnockoutPrediction>> predsByUserAndSlot,
        Dictionary<(string, string), int> memo)
    {
        if (memo.TryGetValue((userId, slotKey), out var cached))
            return cached;

        var result = 0;
        if (slotByKey.TryGetValue(slotKey, out var slot)
            && slot.WinnerTeamId is { } winner
            && predsByUserAndSlot.TryGetValue(userId, out var predsBySlot)
            && predsBySlot.TryGetValue(slotKey, out var pred)
            && pred.PredictedWinnerTeamId == winner)
        {
            var feederKey = FeederSlotKeyFor(slot, winner);
            var streakBefore = feederKey is not null
                ? FullStreak(userId, feederKey, slotByKey, predsByUserAndSlot, memo)
                : 0;
            result = streakBefore + 1;
        }

        memo[(userId, slotKey)] = result;
        return result;
    }

    // The preceding knockout match the winning team came from, or null when the team
    // entered from a group placement (R32) or via a losers' bracket (third-place play-off).
    internal static string? FeederSlotKeyFor(KnockoutSlot slot, string winnerTeamId)
    {
        var source = winnerTeamId == slot.HomeTeamId ? slot.HomeSlotSource
                   : winnerTeamId == slot.AwayTeamId ? slot.AwaySlotSource
                   : null;

        return source is { Type: SlotSourceType.MatchWinner } ? source.Reference : null;
    }
}
