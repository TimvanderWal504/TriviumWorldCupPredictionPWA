using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Scoring;

/// <summary>
/// Recomputes and persists a <see cref="MemberScore"/> document for every member
/// who has any prediction.  The operation is idempotent — running it multiple
/// times on the same data yields identical totals.
///
/// Pipeline:
///   1. Load all completed group-stage fixtures.
///   2. Load all GroupPredictions; score each against its fixture result.
///   3. Load all TournamentPredictions; compute champion + Golden Six points.
///   4. Upsert one MemberScore per member.
/// </summary>
public class ScoringRecomputeService(IDocumentStore store)
{
    public async Task RecomputeAllAsync(CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();

        // ── Step 1: Accumulate group-match points ─────────────────────────────

        // Load completed group-stage fixtures that have results.
        var completedFixtures = await session
            .Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Completed
                        && f.HomeScore != null
                        && f.AwayScore != null)
            .ToListAsync(ct);

        var fixtureById = completedFixtures.ToDictionary(f => f.Id);

        // All group predictions.
        var allGroupPredictions = await session
            .Query<GroupPrediction>()
            .ToListAsync(ct);

        // Per-member accumulators: keyed by UserId.
        var groupMatchPoints   = new Dictionary<string, int>();
        var exactCount         = new Dictionary<string, int>();
        var correctOutcomeCount = new Dictionary<string, int>();

        foreach (var pred in allGroupPredictions)
        {
            if (!fixtureById.TryGetValue(pred.FixtureId, out var fixture))
                continue; // fixture not yet completed — skip

            var actualHome = fixture.HomeScore!.Value;
            var actualAway = fixture.AwayScore!.Value;

            var pts = GroupMatchScorer.Compute(pred.HomeScore, pred.AwayScore, actualHome, actualAway);

            groupMatchPoints[pred.UserId]    = groupMatchPoints.GetValueOrDefault(pred.UserId)    + pts;
            exactCount[pred.UserId]          = exactCount.GetValueOrDefault(pred.UserId)          + (GroupMatchScorer.IsExact(pred.HomeScore, pred.AwayScore, actualHome, actualAway) ? 1 : 0);
            correctOutcomeCount[pred.UserId] = correctOutcomeCount.GetValueOrDefault(pred.UserId) + (pts >= 3 ? 1 : 0);
        }

        // ── Step 2: Tournament predictions — champion + Golden Six ────────────

        var tournamentPredictions = await session
            .Query<TournamentPrediction>()
            .ToListAsync(ct);

        // Find champion: the Final knockout slot with a winner.
        var finalSlot = await session
            .Query<KnockoutSlot>()
            .Where(s => s.Round == Round.Final
                        && s.Status == MatchStatus.Completed
                        && s.WinnerTeamId != null)
            .FirstOrDefaultAsync(ct);

        var tournamentChampionTeamId = finalSlot?.WinnerTeamId;

        // Compute per-player goal counts from GoalEvents (exclude Shootout + OwnGoal).
        var countableGoals = await session
            .Query<GoalEvent>()
            .Where(g => g.Type != GoalType.Shootout && g.Type != GoalType.OwnGoal)
            .ToListAsync(ct);

        var goalCountByPlayer = countableGoals
            .GroupBy(g => g.PlayerId)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        // Resolve Player positions for every player referenced in any Golden Six pick.
        var allPickedPlayerIds = tournamentPredictions
            .SelectMany(tp => tp.GoldenSixPlayerIds)
            .Distinct()
            .ToList();

        var playerDocs = await session
            .Query<Player>()
            .Where(p => p.Id.IsOneOf(allPickedPlayerIds))
            .ToListAsync(ct);

        var positionById = playerDocs.ToDictionary(p => p.Id, p => p.Position);

        // Build the playerStats dictionary for GoldenSixScorer.
        // Only include players that are picked AND have a known position.
        var playerStats = allPickedPlayerIds
            .Where(id => positionById.ContainsKey(id))
            .ToDictionary(
                id => id,
                id => (position: positionById[id], goals: goalCountByPlayer.GetValueOrDefault(id)));

        var championPointsByUser  = new Dictionary<string, int>();
        var goldenSixPointsByUser = new Dictionary<string, int>();

        foreach (var tp in tournamentPredictions)
        {
            // Champion points: 100 if the predicted team is the tournament winner.
            var champPts = tournamentChampionTeamId is not null
                           && tp.ChampionTeamId == tournamentChampionTeamId
                ? 100
                : 0;
            championPointsByUser[tp.UserId] = champPts;

            // Golden Six points.
            var gs6Pts = GoldenSixScorer.ComputeTotal(playerStats, tp.GoldenSixPlayerIds);
            goldenSixPointsByUser[tp.UserId] = gs6Pts;
        }

        // ── Step 3: Gather all user IDs and upsert MemberScore documents ──────

        var allUserIds = groupMatchPoints.Keys
            .Union(championPointsByUser.Keys)
            .Union(goldenSixPointsByUser.Keys)
            .Distinct();

        var now = DateTimeOffset.UtcNow;

        foreach (var userId in allUserIds)
        {
            var score = new MemberScore
            {
                Id                  = userId,
                UserId              = userId,
                GroupMatchPoints    = groupMatchPoints.GetValueOrDefault(userId),
                ChampionPoints      = championPointsByUser.GetValueOrDefault(userId),
                GoldenSixPoints     = goldenSixPointsByUser.GetValueOrDefault(userId),
                ExactScorelineCount = exactCount.GetValueOrDefault(userId),
                CorrectOutcomeCount = correctOutcomeCount.GetValueOrDefault(userId),
                LastComputedAt      = now,
            };

            session.Store(score);
        }

        await session.SaveChangesAsync(ct);
    }
}
