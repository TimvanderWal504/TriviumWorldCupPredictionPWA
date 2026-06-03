using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Pure static scoring logic for the Golden Six prediction.
/// No database dependency — fully unit-testable with plain values.
///
/// Points per goal by position:
///   Forward:    3 pts/goal
///   Midfielder: 5 pts/goal
///   Defender:   8 pts/goal
///   Goalkeeper: 15 pts/goal
///
/// Counted: open play + in-match penalty kicks (regular time + extra time).
/// NOT counted: penalty-shootout goals, own goals.
/// Position is fixed to what is stored in the Player document.
/// </summary>
public static class GoldenSixScorer
{
    /// <summary>Returns the points earned per goal for the given position.</summary>
    public static int PointsPerGoal(Position position) => position switch
    {
        Position.FWD => 3,
        Position.MID => 5,
        Position.DEF => 8,
        Position.GK  => 15,
        _            => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown position.")
    };

    /// <summary>Returns the total Golden Six points for a single player given their position and goal count.</summary>
    public static int ComputeForPlayer(Position position, int goals)
        => PointsPerGoal(position) * goals;

    /// <summary>
    /// Returns the total Golden Six points for a member's six picked players.
    /// Only players whose IDs appear in <paramref name="pickedPlayerIds"/> are counted.
    /// Players not present in <paramref name="playerStats"/> contribute 0 points.
    /// </summary>
    /// <param name="playerStats">
    /// Map of PlayerId → (position, goals) for all players with recorded goals.
    /// </param>
    /// <param name="pickedPlayerIds">
    /// The six player IDs the member selected as their Golden Six.
    /// </param>
    public static int ComputeTotal(
        IReadOnlyDictionary<Guid, (Position position, int goals)> playerStats,
        IEnumerable<Guid> pickedPlayerIds)
    {
        var total = 0;
        foreach (var playerId in pickedPlayerIds)
        {
            if (playerStats.TryGetValue(playerId, out var stats))
                total += ComputeForPlayer(stats.position, stats.goals);
            // players with no goals (not in dict) contribute 0
        }
        return total;
    }
}
