using Marten;
using Microsoft.AspNetCore.OutputCaching;
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
public class ScoringRecomputeService(IDocumentStore store, IOutputCacheStore outputCache)
{
    /// <summary>
    /// Full recompute for every member. Prefer <see cref="RecomputeForCompletedAsync"/>
    /// when only a specific fixture or slot just completed — it restricts the work to
    /// the users who actually predicted that match.
    /// </summary>
    public Task RecomputeAllAsync(CancellationToken ct = default)
        => RecomputeCoreAsync(restrictToUserIds: null, ct);

    /// <summary>
    /// Shared-session overload — accumulates MemberScore stores into
    /// <paramref name="writeSession"/> without calling SaveChangesAsync.
    /// Parallel reads still open their own short-lived sessions (Marten
    /// lightweight sessions are not thread-safe). Caller is responsible for flushing.
    /// </summary>
    public Task RecomputeAllAsync(IDocumentSession writeSession, CancellationToken ct = default)
        => RecomputeCoreAsync(restrictToUserIds: null, ct, writeSession);

    /// <summary>
    /// Targeted recompute: resolves which users predicted any of the supplied completed
    /// fixtures / knockout slots, then rescores only those users. When the Final completes
    /// ("FIN"), all users with a TournamentPrediction are also included because their
    /// champion points change.
    /// </summary>
    public Task RecomputeForCompletedAsync(
        IReadOnlyCollection<string> completedFixtureIds,
        IReadOnlyCollection<string> completedSlotKeys,
        CancellationToken ct = default)
        => RecomputeForCompletedCoreAsync(completedFixtureIds, completedSlotKeys, null, ct);

    /// <summary>
    /// Shared-session overload — accumulates MemberScore stores into
    /// <paramref name="writeSession"/> without calling SaveChangesAsync.
    /// Caller is responsible for flushing.
    /// </summary>
    public Task RecomputeForCompletedAsync(
        IReadOnlyCollection<string> completedFixtureIds,
        IReadOnlyCollection<string> completedSlotKeys,
        IDocumentSession writeSession,
        CancellationToken ct = default)
        => RecomputeForCompletedCoreAsync(completedFixtureIds, completedSlotKeys, writeSession, ct);

    private async Task RecomputeForCompletedCoreAsync(
        IReadOnlyCollection<string> completedFixtureIds,
        IReadOnlyCollection<string> completedSlotKeys,
        IDocumentSession? externalWriteSession,
        CancellationToken ct)
    {
        async Task<T> QueryAsync<T>(Func<IQuerySession, Task<T>> query)
        {
            await using var s = store.LightweightSession();
            return await query(s);
        }

        // Resolve affected user IDs from group and knockout predictions in parallel.
        var groupUserIdsTask = completedFixtureIds.Count > 0
            ? QueryAsync(s => s.Query<GroupPrediction>()
                .Where(p => p.FixtureId.IsOneOf(completedFixtureIds.ToList()))
                .Select(p => p.UserId)
                .ToListAsync(ct))
            : Task.FromResult<IReadOnlyList<string>>([]);

        var knockoutUserIdsTask = completedSlotKeys.Count > 0
            ? QueryAsync(s => s.Query<KnockoutPrediction>()
                .Where(p => p.SlotKey.IsOneOf(completedSlotKeys.ToList()))
                .Select(p => p.UserId)
                .ToListAsync(ct))
            : Task.FromResult<IReadOnlyList<string>>([]);

        await Task.WhenAll(groupUserIdsTask, knockoutUserIdsTask);

        var affectedUserIds = new HashSet<string>(await groupUserIdsTask);
        affectedUserIds.UnionWith(await knockoutUserIdsTask);

        // The Final completing changes champion points for everyone with a TournamentPrediction.
        if (completedSlotKeys.Contains("FIN"))
        {
            var tournamentUserIds = await QueryAsync(s => s.Query<TournamentPrediction>()
                .Select(tp => tp.UserId)
                .ToListAsync(ct));
            affectedUserIds.UnionWith(tournamentUserIds);
        }

        if (affectedUserIds.Count == 0)
            return;

        await RecomputeCoreAsync(affectedUserIds, ct, externalWriteSession);
    }

    private async Task RecomputeCoreAsync(IReadOnlySet<string>? restrictToUserIds, CancellationToken ct, IDocumentSession? externalWriteSession = null)
    {
        // Marten lightweight sessions are not thread-safe, so each parallel
        // query gets its own session.
        async Task<T> QueryAsync<T>(Func<IQuerySession, Task<T>> query)
        {
            await using var s = store.LightweightSession();
            return await query(s);
        }

        // ── Wave 1: all mutually-independent reads in parallel ────────────────

        var fixturesTask = QueryAsync(s => s.Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Completed
                        && f.HomeScore != null
                        && f.AwayScore != null)
            .ToListAsync(ct));

        var groupPredsTask = QueryAsync(s =>
        {
            var q = s.Query<GroupPrediction>();
            return (restrictToUserIds != null
                ? q.Where(p => p.UserId.IsOneOf(restrictToUserIds.ToList()))
                : q).ToListAsync(ct);
        });

        var tournamentPredsTask = QueryAsync(s =>
        {
            var q = s.Query<TournamentPrediction>();
            return (restrictToUserIds != null
                ? q.Where(tp => tp.UserId.IsOneOf(restrictToUserIds.ToList()))
                : q).ToListAsync(ct);
        });

        var goalsTask = QueryAsync(s => s.Query<GoalEvent>()
            .Where(g => g.Type != GoalType.Shootout && g.Type != GoalType.OwnGoal)
            .ToListAsync(ct));
        // completedKnockoutSlots covers the Final too — no separate finalSlot query needed.
        var knockoutSlotsTask = QueryAsync(s => s.Query<KnockoutSlot>()
            .Where(k => k.Status == MatchStatus.Completed && k.WinnerTeamId != null)
            .ToListAsync(ct));

        var knockoutPredsTask = QueryAsync(s =>
        {
            var q = s.Query<KnockoutPrediction>();
            return (restrictToUserIds != null
                ? q.Where(p => p.UserId.IsOneOf(restrictToUserIds.ToList()))
                : q).ToListAsync(ct);
        });

        await Task.WhenAll(fixturesTask, groupPredsTask, tournamentPredsTask,
                           goalsTask, knockoutSlotsTask, knockoutPredsTask);

        var completedFixtures      = await fixturesTask;
        var allGroupPredictions    = await groupPredsTask;
        var tournamentPredictions  = await tournamentPredsTask;
        var countableGoals         = await goalsTask;
        var completedKnockoutSlots = await knockoutSlotsTask;
        var allKnockoutPredictions = await knockoutPredsTask;

        // ── Wave 2: Players — depends on tournament predictions ───────────────

        var allPickedPlayerIds = tournamentPredictions
            .SelectMany(tp => tp.GoldenSixPlayerIds)
            .Distinct()
            .ToList();

        IReadOnlyList<Player> playerDocs = allPickedPlayerIds.Count > 0
            ? await QueryAsync(s => s.Query<Player>()
                .Where(p => p.Id.IsOneOf(allPickedPlayerIds))
                .ToListAsync(ct))
            : Array.Empty<Player>();

        // ── Step 1: Accumulate group-match points ─────────────────────────────

        var fixtureById = completedFixtures.ToDictionary(f => f.Id);

        var groupMatchPoints    = new Dictionary<string, int>();
        var exactCount          = new Dictionary<string, int>();
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

        // Final slot is already in completedKnockoutSlots — no extra round trip.
        var tournamentChampionTeamId = completedKnockoutSlots
            .FirstOrDefault(s => s.Round == Round.Final)
            ?.WinnerTeamId;

        var goalCountByPlayer = countableGoals
            .GroupBy(g => g.PlayerId)
            .ToDictionary(grp => grp.Key, grp => grp.Count());

        var positionById = playerDocs.ToDictionary(p => p.Id, p => p.Position);

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

            var gs6Pts = GoldenSixScorer.ComputeTotal(playerStats, tp.GoldenSixPlayerIds);
            goldenSixPointsByUser[tp.UserId] = gs6Pts;
        }

        // ── Step 3: Knockout match points (streak-multiplied) ─────────────────
        //
        // Process each user's predictions in tournament order (R32 → R16 → QF → SF →
        // ThirdPlace → Final). A running per-user streak tracks consecutive correct
        // advancing-team predictions; a wrong prediction resets the streak to 0.
        // The advancing-team bonus for match N in a user's streak is 5 × (streak + 1).

        var slotByKey = completedKnockoutSlots.ToDictionary(s => s.SlotKey);

        // Canonical slot order: Round enum values ascend in tournament order;
        // within a round, ordered by SlotNumber.
        var orderedSlotKeys = completedKnockoutSlots
            .OrderBy(s => s.Round)
            .ThenBy(s => s.SlotNumber)
            .Select(s => s.SlotKey)
            .ToList();

        // Group each user's predictions by slot key for O(1) lookup.
        var predsByUserAndSlot = allKnockoutPredictions
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(p => p.SlotKey));

        var knockoutPointsByUser = new Dictionary<string, int>();
        var streakByUser         = new Dictionary<string, int>();

        foreach (var slotKey in orderedSlotKeys)
        {
            var slot = slotByKey[slotKey];

            foreach (var (userId, predsBySlot) in predsByUserAndSlot)
            {
                if (!predsBySlot.TryGetValue(slotKey, out var pred))
                    continue; // no prediction for this slot — skip (streak unchanged)

                var streakBefore = streakByUser.GetValueOrDefault(userId, 0);

                var pts = KnockoutMatchScorer.Compute(
                    pred.PredictedWinnerTeamId,
                    pred.PredictedHomeScore, pred.PredictedAwayScore,
                    slot.WinnerTeamId!,
                    slot.HomeScore, slot.AwayScore,
                    streakBefore);

                knockoutPointsByUser[userId] = knockoutPointsByUser.GetValueOrDefault(userId) + pts;

                streakByUser[userId] = pred.PredictedWinnerTeamId == slot.WinnerTeamId
                    ? streakBefore + 1
                    : 0;
            }
        }

        // ── Step 4: Gather all user IDs and upsert MemberScore documents ──────

        var allUserIds = groupMatchPoints.Keys
            .Union(championPointsByUser.Keys)
            .Union(goldenSixPointsByUser.Keys)
            .Union(knockoutPointsByUser.Keys)
            .Distinct();

        var now = DateTimeOffset.UtcNow;

        // Use the caller-supplied session when batching with the ingestion job;
        // otherwise open and flush our own.
        IDocumentSession ownedSession = externalWriteSession is null
            ? store.LightweightSession()
            : null!;
        var writeSession = externalWriteSession ?? ownedSession;

        try
        {
            foreach (var userId in allUserIds)
            {
                var score = new MemberScore
                {
                    Id                  = userId,
                    UserId              = userId,
                    GroupMatchPoints    = groupMatchPoints.GetValueOrDefault(userId),
                    ChampionPoints      = championPointsByUser.GetValueOrDefault(userId),
                    GoldenSixPoints     = goldenSixPointsByUser.GetValueOrDefault(userId),
                    KnockoutPoints      = knockoutPointsByUser.GetValueOrDefault(userId),
                    ExactScorelineCount = exactCount.GetValueOrDefault(userId),
                    CorrectOutcomeCount = correctOutcomeCount.GetValueOrDefault(userId),
                    LastComputedAt      = now,
                };

                writeSession.Store(score);
            }

            if (externalWriteSession is null)
            {
                await writeSession.SaveChangesAsync(ct);
                await outputCache.EvictByTagAsync("leaderboard", ct);
            }
        }
        finally
        {
            if (ownedSession is not null)
                await ownedSession.DisposeAsync();
        }
    }
}
