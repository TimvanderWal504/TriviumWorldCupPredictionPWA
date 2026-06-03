using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Leaderboard;

/// <summary>
/// Pure static ranking logic for the leaderboard (TWC-11).
/// Applies competition ranking (not dense ranking) with the tiebreaker chain:
///   1. TotalPoints DESC
///   2. ExactScorelineCount DESC
///   3. CorrectOutcomeCount DESC
///   4. Still equal → shared rank; next rank skips (e.g. two at rank 1 → next is rank 3).
/// </summary>
public static class LeaderboardRanker
{
    /// <summary>
    /// Assigns competition ranks to the supplied scores and returns
    /// <see cref="RankedScore"/> records in rank order (ascending).
    ///
    /// Members absent from <paramref name="scores"/> do not appear here;
    /// the endpoint layer appends zero-point members at the bottom.
    /// </summary>
    public static IReadOnlyList<RankedScore> Rank(IEnumerable<MemberScore> scores)
    {
        // Sort by the full tiebreaker chain.
        var sorted = scores
            .OrderByDescending(s => s.TotalPoints)
            .ThenByDescending(s => s.ExactScorelineCount)
            .ThenByDescending(s => s.CorrectOutcomeCount)
            .ToList();

        var result = new List<RankedScore>(sorted.Count);

        // Competition (Olympic/standard) ranking: rank = position of first member
        // in the tied group.  Two members at position 1 → next is rank 3, not 2.
        for (var i = 0; i < sorted.Count; i++)
        {
            int rank;
            if (i == 0)
            {
                rank = 1;
            }
            else
            {
                var prev = sorted[i - 1];
                var curr = sorted[i];

                bool tiedWithPrev =
                    curr.TotalPoints         == prev.TotalPoints &&
                    curr.ExactScorelineCount == prev.ExactScorelineCount &&
                    curr.CorrectOutcomeCount == prev.CorrectOutcomeCount;

                rank = tiedWithPrev ? result[i - 1].Rank : i + 1;
            }

            result.Add(new RankedScore(sorted[i], rank));
        }

        return result;
    }
}

/// <summary>A <see cref="MemberScore"/> with its computed competition rank.</summary>
public sealed record RankedScore(MemberScore Score, int Rank);
