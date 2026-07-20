using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Verification;

/// <summary>
/// Independently re-derives every member's points from the raw prediction and result
/// documents, then diffs the result against the persisted <see cref="MemberScore"/>.
///
/// This class deliberately does NOT call GroupMatchScorer, KnockoutMatchScorer,
/// GoldenSixScorer or KnockoutStreakCalculator. It is a second implementation of the
/// scoring rules written against the rule statements themselves, so that a bug in the
/// production scorers shows up as a discrepancy rather than being reproduced faithfully.
/// If you change a scoring rule, change it here too — the two implementations are
/// expected to agree, and a disagreement is the whole signal this class exists to give.
///
/// Rules implemented (as of 20 July 2026):
///   Group match — best single tier: exact 10, correct GD 7, correct outcome 3, wrong 0;
///                 +1 when exactly one team's goal tally was right.
///   Knockout    — Component 1: group tiers on the score at the applicable cutoff
///                 (KnockoutSlot.HomeScore/AwayScore already hold the ET score for AET/PEN
///                 matches, see TWC-83); never multiplied.
///                 Component 2: 5 x (streakBefore + 1) when the advancing team is right.
///                 The streak follows the ADVANCING TEAM along its bracket path, not the
///                 user's global run of correct picks: it counts back through MatchWinner
///                 feeder slots only. R32 and the third-place play-off always start fresh.
///   Champion    — 100 when the predicted team wins the Final.
///   Golden Six  — per goal by position: FWD 3, MID 5, DEF 8, GK 15.
///                 Shootout goals and own goals never count.
/// </summary>
public class ScoreVerifier(IDocumentStore store)
{
    /// <summary>
    /// Verifies every member, or only <paramref name="userId"/> when supplied.
    /// Read-only — never writes to the database.
    /// </summary>
    public async Task<ScoreVerificationReport> VerifyAsync(
        string? userId = null,
        CancellationToken ct = default)
    {
        await using var session = store.QuerySession();

        var completedFixtures = await session.Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Completed
                        && f.HomeScore != null
                        && f.AwayScore != null)
            .ToListAsync(ct);

        var knockoutSlots = await session.Query<KnockoutSlot>().ToListAsync(ct);
        var players       = await session.Query<Player>().ToListAsync(ct);

        // Load every goal and filter in memory, so a wrong server-side filter in the
        // production query shows up as a difference rather than being inherited.
        var allGoals = await session.Query<GoalEvent>().ToListAsync(ct);

        var groupPreds      = await LoadAsync<GroupPrediction>(session, userId, p => p.UserId, ct);
        var knockoutPreds   = await LoadAsync<KnockoutPrediction>(session, userId, p => p.UserId, ct);
        var tournamentPreds = await LoadAsync<TournamentPrediction>(session, userId, p => p.UserId, ct);
        var storedScores    = await LoadAsync<MemberScore>(session, userId, s => s.UserId, ct);

        return BuildReport(completedFixtures, knockoutSlots, players, allGoals,
                           groupPreds, knockoutPreds, tournamentPreds, storedScores);
    }

    /// <summary>
    /// Pure comparison over already-loaded documents — no database access.
    /// Exposed for tests; <see cref="VerifyAsync"/> is the production entry point.
    /// </summary>
    internal static ScoreVerificationReport BuildReport(
        IReadOnlyList<Fixture> completedFixtures,
        IReadOnlyList<KnockoutSlot> knockoutSlots,
        IReadOnlyList<Player> players,
        IReadOnlyList<GoalEvent> allGoals,
        IReadOnlyList<GroupPrediction> groupPreds,
        IReadOnlyList<KnockoutPrediction> knockoutPreds,
        IReadOnlyList<TournamentPrediction> tournamentPreds,
        IReadOnlyList<MemberScore> storedScores)
    {
        var expected = Derive(completedFixtures, knockoutSlots, players, allGoals,
                              groupPreds, knockoutPreds, tournamentPreds);

        var storedById = storedScores.ToDictionary(s => s.UserId);

        var allUserIds = new HashSet<string>(expected.Keys);
        allUserIds.UnionWith(storedById.Keys);

        var results = new List<UserScoreVerification>();

        foreach (var id in allUserIds.OrderBy(x => x, StringComparer.Ordinal))
        {
            expected.TryGetValue(id, out var exp);
            storedById.TryGetValue(id, out var stored);

            // A member with no predictions and an all-zero score document is not a problem;
            // only flag an orphan that actually carries points.
            if (exp is null)
            {
                if (stored is not null && stored.TotalPoints != 0)
                {
                    results.Add(new UserScoreVerification(
                        id, false, true, stored.LastComputedAt,
                        [new FieldDiscrepancy("TotalPoints", stored.TotalPoints, 0)],
                        []));
                }
                continue;
            }

            if (stored is null)
            {
                // Only a genuine problem when the member should have scored something.
                if (exp.Total != 0)
                {
                    results.Add(new UserScoreVerification(
                        id, true, false, null,
                        [new FieldDiscrepancy("TotalPoints", 0, exp.Total)],
                        []));
                }
                continue;
            }

            var totals     = CompareTotals(stored, exp);
            var perPredict = ComparePredictions(stored, exp);

            if (totals.Count > 0 || perPredict.Count > 0)
            {
                results.Add(new UserScoreVerification(
                    id, false, false, stored.LastComputedAt, totals, perPredict));
            }
        }

        return new ScoreVerificationReport(
            VerifiedAt:            DateTimeOffset.UtcNow,
            UsersChecked:          allUserIds.Count,
            UsersWithDiscrepancies: results.Count,
            TotalDiscrepancies:    results.Sum(r => r.Totals.Count + r.Predictions.Count),
            Users:                 results);
    }

    private static async Task<IReadOnlyList<T>> LoadAsync<T>(
        IQuerySession session,
        string? userId,
        System.Linq.Expressions.Expression<Func<T, string>> userIdSelector,
        CancellationToken ct) where T : notnull
    {
        var query = session.Query<T>();
        if (userId is null)
            return await query.ToListAsync(ct);

        // Build "x => selector(x) == userId" without duplicating each document type's shape.
        var param = userIdSelector.Parameters[0];
        var body  = System.Linq.Expressions.Expression.Equal(
            userIdSelector.Body,
            System.Linq.Expressions.Expression.Constant(userId));
        var predicate = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);

        return await query.Where(predicate).ToListAsync(ct);
    }

    // ── Comparison ────────────────────────────────────────────────────────────

    private static List<FieldDiscrepancy> CompareTotals(MemberScore stored, ExpectedScore exp)
    {
        var diffs = new List<FieldDiscrepancy>();

        void Check(string field, int storedValue, int expectedValue)
        {
            if (storedValue != expectedValue)
                diffs.Add(new FieldDiscrepancy(field, storedValue, expectedValue));
        }

        Check("GroupMatchPoints",    stored.GroupMatchPoints,    exp.GroupMatchPoints);
        Check("KnockoutPoints",      stored.KnockoutPoints,      exp.KnockoutPoints);
        Check("ChampionPoints",      stored.ChampionPoints,      exp.ChampionPoints);
        Check("GoldenSixPoints",     stored.GoldenSixPoints,     exp.GoldenSixPoints);
        Check("ExactScorelineCount", stored.ExactScorelineCount, exp.ExactScorelineCount);
        Check("CorrectOutcomeCount", stored.CorrectOutcomeCount, exp.CorrectOutcomeCount);
        Check("TotalPoints",         stored.TotalPoints,         exp.Total);

        // Internal consistency: the stored breakdown lists must sum to the stored totals.
        // These are what the leaderboard, results and drill-down endpoints actually read,
        // so a breakdown that disagrees with its own total is a user-visible bug even when
        // the total itself is correct.
        Check("GroupBreakdownSum",
            stored.GroupBreakdown.Sum(b => b.Points), stored.GroupMatchPoints);
        Check("KnockoutBreakdownSum",
            stored.KnockoutBreakdown.Sum(b => b.ScorePoints + b.AdvancingPoints), stored.KnockoutPoints);
        Check("GoldenSixBreakdownSum",
            stored.GoldenSixBreakdown.Sum(b => b.Points), stored.GoldenSixPoints);

        return diffs;
    }

    private static List<PredictionDiscrepancy> ComparePredictions(MemberScore stored, ExpectedScore exp)
    {
        var diffs = new List<PredictionDiscrepancy>();

        var storedGroup = stored.GroupBreakdown.ToDictionary(b => b.FixtureId);
        foreach (var (fixtureId, e) in exp.Group.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!storedGroup.TryGetValue(fixtureId, out var s))
            {
                diffs.Add(new PredictionDiscrepancy("group", fixtureId,
                    $"{e.Detail} — missing from stored breakdown", 0, e.Points));
            }
            else if (s.Points != e.Points)
            {
                diffs.Add(new PredictionDiscrepancy("group", fixtureId, e.Detail, s.Points, e.Points));
            }
        }
        foreach (var s in storedGroup.Values.Where(s => !exp.Group.ContainsKey(s.FixtureId)))
        {
            diffs.Add(new PredictionDiscrepancy("group", s.FixtureId,
                "stored breakdown entry has no matching prediction/result", s.Points, 0));
        }

        var storedKo = stored.KnockoutBreakdown.ToDictionary(b => b.SlotKey);
        foreach (var (slotKey, e) in exp.Knockout.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!storedKo.TryGetValue(slotKey, out var s))
            {
                diffs.Add(new PredictionDiscrepancy("knockout", slotKey,
                    $"{e.Detail} — missing from stored breakdown", 0, e.Points));
                continue;
            }

            var storedTotal = s.ScorePoints + s.AdvancingPoints;
            if (storedTotal != e.Points)
            {
                diffs.Add(new PredictionDiscrepancy("knockout", slotKey, e.Detail, storedTotal, e.Points));
            }
            else if (s.StreakMultiplier != e.StreakMultiplier)
            {
                // Same total but a different multiplier means the two components are
                // individually wrong and cancelled out — still worth surfacing.
                diffs.Add(new PredictionDiscrepancy("knockout", slotKey,
                    $"{e.Detail} — total matches but streak multiplier differs",
                    s.StreakMultiplier, e.StreakMultiplier));
            }
        }
        foreach (var s in storedKo.Values.Where(s => !exp.Knockout.ContainsKey(s.SlotKey)))
        {
            diffs.Add(new PredictionDiscrepancy("knockout", s.SlotKey,
                "stored breakdown entry has no matching prediction/result",
                s.ScorePoints + s.AdvancingPoints, 0));
        }

        var storedGs6 = stored.GoldenSixBreakdown.ToDictionary(b => b.PlayerId);
        foreach (var (playerId, e) in exp.GoldenSix)
        {
            if (!storedGs6.TryGetValue(playerId, out var s))
            {
                diffs.Add(new PredictionDiscrepancy("goldensix", playerId.ToString(),
                    $"{e.Detail} — missing from stored breakdown", 0, e.Points));
            }
            else if (s.Points != e.Points || s.Goals != e.Goals)
            {
                diffs.Add(new PredictionDiscrepancy("goldensix", playerId.ToString(),
                    $"{e.Detail} — stored {s.Goals} goals", s.Points, e.Points));
            }
        }

        return diffs;
    }

    // ── Independent derivation ────────────────────────────────────────────────

    private static Dictionary<string, ExpectedScore> Derive(
        IReadOnlyList<Fixture> completedFixtures,
        IReadOnlyList<KnockoutSlot> knockoutSlots,
        IReadOnlyList<Player> players,
        IReadOnlyList<GoalEvent> allGoals,
        IReadOnlyList<GroupPrediction> groupPreds,
        IReadOnlyList<KnockoutPrediction> knockoutPreds,
        IReadOnlyList<TournamentPrediction> tournamentPreds)
    {
        var byUser = new Dictionary<string, ExpectedScore>();

        ExpectedScore For(string userId)
        {
            if (!byUser.TryGetValue(userId, out var e))
                byUser[userId] = e = new ExpectedScore();
            return e;
        }

        // ── Group stage ───────────────────────────────────────────────────────

        var fixtureById = completedFixtures.ToDictionary(f => f.Id);

        foreach (var pred in groupPreds)
        {
            if (!fixtureById.TryGetValue(pred.FixtureId, out var fixture))
                continue; // no result yet — nothing to score

            var actualHome = fixture.HomeScore!.Value;
            var actualAway = fixture.AwayScore!.Value;

            var pts = MatchPoints(pred.HomeScore, pred.AwayScore, actualHome, actualAway);

            var e = For(pred.UserId);
            e.GroupMatchPoints += pts;
            if (pred.HomeScore == actualHome && pred.AwayScore == actualAway)
                e.ExactScorelineCount++;
            if (pts >= 3)
                e.CorrectOutcomeCount++;

            e.Group[pred.FixtureId] = new ExpectedPrediction(
                pts,
                $"predicted {pred.HomeScore}-{pred.AwayScore}, actual {actualHome}-{actualAway}");
        }

        // ── Knockout ──────────────────────────────────────────────────────────

        var completedSlots = knockoutSlots
            .Where(s => s.Status == MatchStatus.Completed && s.WinnerTeamId != null)
            .ToList();

        var slotByKey = completedSlots.ToDictionary(s => s.SlotKey);

        var predBySlotAndUser = knockoutPreds
            .GroupBy(p => p.SlotKey)
            .ToDictionary(g => g.Key, g => g.ToDictionary(p => p.UserId));

        // Walk the bracket forward in round order so each slot's MatchWinner feeder has
        // already been evaluated by the time we need its streak. This is the same rule as
        // the production recursive-memo implementation, derived the other way round.
        var streakAt = new Dictionary<(string UserId, string SlotKey), int>();

        var orderedSlots = completedSlots
            .OrderBy(s => (int)s.Round)
            .ThenBy(s => s.SlotNumber)
            .ToList();

        foreach (var slot in orderedSlots)
        {
            if (!predBySlotAndUser.TryGetValue(slot.SlotKey, out var predsForSlot))
                continue;

            var winner     = slot.WinnerTeamId!;
            var feederKey  = WinnerFeederSlotKey(slot, winner);

            foreach (var (userId, pred) in predsForSlot)
            {
                var correctWinner = pred.PredictedWinnerTeamId == winner;

                var streakBefore = 0;
                if (correctWinner && feederKey is not null
                    && streakAt.TryGetValue((userId, feederKey), out var feederStreak))
                {
                    streakBefore = feederStreak;
                }

                var chain = correctWinner ? streakBefore + 1 : 0;
                streakAt[(userId, slot.SlotKey)] = chain;

                var scorePoints = pred.PredictedHomeScore.HasValue && pred.PredictedAwayScore.HasValue
                                  && slot.HomeScore.HasValue && slot.AwayScore.HasValue
                    ? MatchPoints(pred.PredictedHomeScore.Value, pred.PredictedAwayScore.Value,
                                  slot.HomeScore.Value, slot.AwayScore.Value)
                    : 0;

                var advancingPoints = correctWinner ? 5 * chain : 0;

                var e = For(userId);
                e.KnockoutPoints += scorePoints + advancingPoints;
                e.Knockout[slot.SlotKey] = new ExpectedKnockout(
                    scorePoints + advancingPoints,
                    chain,
                    $"predicted winner {pred.PredictedWinnerTeamId}, actual {winner}; " +
                    $"score {scorePoints} + advancing {advancingPoints} (x{chain})");
            }
        }

        // ── Champion + Golden Six ─────────────────────────────────────────────

        var championTeamId = completedSlots
            .FirstOrDefault(s => s.Round == Round.Final)
            ?.WinnerTeamId;

        var positionById = players.ToDictionary(p => p.Id, p => p.Position);

        var goalsByPlayer = allGoals
            .Where(g => g.Type != GoalType.Shootout && g.Type != GoalType.OwnGoal)
            .GroupBy(g => g.PlayerId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var tp in tournamentPreds)
        {
            var e = For(tp.UserId);

            e.ChampionPoints = championTeamId is not null && tp.ChampionTeamId == championTeamId
                ? 100
                : 0;

            foreach (var playerId in tp.GoldenSixPlayerIds)
            {
                if (!positionById.TryGetValue(playerId, out var position))
                    continue; // unknown player contributes nothing

                var goals = goalsByPlayer.GetValueOrDefault(playerId);
                var pts   = PointsPerGoal(position) * goals;

                e.GoldenSixPoints += pts;
                e.GoldenSix[playerId] = new ExpectedGoldenSix(
                    pts, goals, $"{position} with {goals} countable goal(s)");
            }
        }

        return byUser;
    }

    /// <summary>
    /// Points for one predicted scoreline against an actual scoreline. Awards the single
    /// best tier, plus the team-tally bonus where it can apply.
    /// </summary>
    private static int MatchPoints(int predHome, int predAway, int actualHome, int actualAway)
    {
        if (predHome == actualHome && predAway == actualAway)
            return 10;

        if (predHome - predAway == actualHome - actualAway)
            return 7; // correct goal difference; no tally can also match without being exact

        // +1 when exactly one team's tally was right.
        var tallyBonus = (predHome == actualHome) ^ (predAway == actualAway) ? 1 : 0;

        var sameOutcome = Math.Sign(predHome - predAway) == Math.Sign(actualHome - actualAway);

        return (sameOutcome ? 3 : 0) + tallyBonus;
    }

    private static int PointsPerGoal(Position position) => position switch
    {
        Position.FWD => 3,
        Position.MID => 5,
        Position.DEF => 8,
        Position.GK  => 15,
        _            => 0,
    };

    /// <summary>
    /// The knockout slot the winning team advanced FROM, or null when it entered the
    /// bracket from a group placement (R32) or via a MatchLoser feed (third-place play-off).
    /// </summary>
    private static string? WinnerFeederSlotKey(KnockoutSlot slot, string winnerTeamId)
    {
        var source = winnerTeamId == slot.HomeTeamId ? slot.HomeSlotSource
                   : winnerTeamId == slot.AwayTeamId ? slot.AwaySlotSource
                   : null;

        return source is { Type: SlotSourceType.MatchWinner } ? source.Reference : null;
    }

    // ── Accumulator ───────────────────────────────────────────────────────────

    private sealed class ExpectedScore
    {
        public int GroupMatchPoints;
        public int KnockoutPoints;
        public int ChampionPoints;
        public int GoldenSixPoints;
        public int ExactScorelineCount;
        public int CorrectOutcomeCount;

        public int Total => GroupMatchPoints + KnockoutPoints + ChampionPoints + GoldenSixPoints;

        public Dictionary<string, ExpectedPrediction> Group    = [];
        public Dictionary<string, ExpectedKnockout>   Knockout = [];
        public Dictionary<Guid,   ExpectedGoldenSix>  GoldenSix = [];
    }

    private sealed record ExpectedPrediction(int Points, string Detail);
    private sealed record ExpectedKnockout(int Points, int StreakMultiplier, string Detail);
    private sealed record ExpectedGoldenSix(int Points, int Goals, string Detail);
}
