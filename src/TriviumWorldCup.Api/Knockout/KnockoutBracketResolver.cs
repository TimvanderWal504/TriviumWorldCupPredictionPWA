using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Knockout;

/// <summary>
/// Resolves the knockout bracket from group-stage results and propagates completed
/// knockout slots into subsequent rounds.
///
/// Idempotent: calling ResolveAsync multiple times on the same data produces the
/// same bracket state — all writes use Marten's Store() (upsert semantics).
///
/// Pipeline:
///   1. ResolveGroupStageAsync — determines winner, runner-up and 3rd-placed team
///      for each group, then populates all R32 slots.  Called once all group-stage
///      fixtures are completed.
///   2. PropagateKnockoutResultAsync — after a knockout slot is recorded (WinnerTeamId
///      set), fills the HomeTeamId/AwayTeamId of any downstream slot that depends on
///      the winner (MatchWinner) or loser (MatchLoser) of the completed slot.
///
/// FIFA ranking criteria (group stage, in order):
///   1. Points (3/1/0)
///   2. Goal difference across all group games
///   3. Goals scored across all group games
///   4. Head-to-head points between tied teams
///   5. Head-to-head goal difference between tied teams
///   6. Head-to-head goals scored between tied teams
///   7+ Disciplinary / FIFA ranking — treated as equal for this implementation.
///
/// Best-third-place allocation:
///   8 of 12 third-placed teams qualify.  Each R32 BestThirdPlace slot covers a set
///   of three groups encoded in the SlotSource.Reference (e.g. "B/C/D").  The
///   assignment is: for each qualifying 3rd-placed team, find the unique R32 slot
///   whose BestThirdPlace reference contains the team's group letter.
///   This works because the seed encodes exactly 8 x 3-group combinations covering
///   all 12 group letters, and the 8 qualifying groups are always a subset that
///   maps bijectively onto those slot references at tournament time.
/// </summary>
public class KnockoutBracketResolver(IDocumentStore store, ILogger<KnockoutBracketResolver> logger)
{
    // -------------------------------------------------------------------------
    // Public entry points
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether all group-stage fixtures are completed and, if so, resolves
    /// the R32 bracket from the current group standings.
    /// Safe to call repeatedly — exits early if not all 72 fixtures are complete.
    /// </summary>
    public async Task ResolveGroupStageAsync(CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        if (await ResolveGroupStageCoreAsync(session, ct) > 0)
            await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Shared-session overload — accumulates stores into <paramref name="session"/> without
    /// calling SaveChangesAsync. Caller is responsible for flushing.
    /// </summary>
    public async Task ResolveGroupStageAsync(IDocumentSession session, CancellationToken ct = default)
        => await ResolveGroupStageCoreAsync(session, ct);

    private async Task<int> ResolveGroupStageCoreAsync(IDocumentSession session, CancellationToken ct)
    {
        // Load all group-stage fixtures.
        var fixtures = await session.Query<Fixture>().ToListAsync(ct);

        // Identify groups where all 6 fixtures are completed — these can be ranked now.
        var completedGroups = fixtures
            .GroupBy(f => f.GroupLetter)
            .Where(g => g.Count() == 6 && g.All(f => f.Status == MatchStatus.Completed))
            .Select(g => g.Key)
            .ToHashSet();

        if (completedGroups.Count == 0)
        {
            logger.LogDebug("KnockoutBracketResolver: no groups fully complete yet — skipping R32 resolution");
            return 0;
        }

        logger.LogInformation(
            "KnockoutBracketResolver: {Done}/12 groups complete — attempting partial R32 resolution",
            completedGroups.Count);

        // Load groups to know which teams are in each group; rank only completed ones.
        var groups = await session.Query<Group>().ToListAsync(ct);
        var rankings = RankAllGroups(groups.Where(g => completedGroups.Contains(g.Letter)), fixtures);

        // Attempt early BestThirdPlace selection: if the real top-8 thirds from completed
        // groups are already unbeatable by any incomplete group's theoretical maximum,
        // inject them now rather than waiting for all 12 groups.
        var incompleteGroups = groups.Where(g => !completedGroups.Contains(g.Letter)).ToList();
        var bestThirds = SelectBestThirdPlaced(rankings, fixtures, incompleteGroups);

        // Load all knockout slots.
        var slots = await session.Query<KnockoutSlot>().ToListAsync(ct);
        var slotByKey = slots.ToDictionary(s => s.SlotKey);

        // Populate R32 slots from group standings.
        var changed = PopulateR32Slots(slotByKey, rankings, bestThirds);

        if (changed > 0)
        {
            foreach (var slot in slotByKey.Values)
                session.Store(slot);

            logger.LogInformation(
                "KnockoutBracketResolver: populated {Count} R32 slot team assignment(s)",
                changed);
        }
        else
        {
            logger.LogDebug("KnockoutBracketResolver: R32 slots already populated — no changes");
        }

        return changed;
    }

    /// <summary>
    /// After a knockout slot result is recorded (WinnerTeamId set on the slot in
    /// Marten), propagates the winner/loser into the downstream slot that references
    /// it.  Also sets WinnerTeamId on the source slot if HomeScore/AwayScore are
    /// present but WinnerTeamId is not yet set (covers the case where the ingestion
    /// job records scores without a winner).
    ///
    /// Safe to call repeatedly — idempotent.
    /// </summary>
    public async Task PropagateKnockoutResultAsync(string completedSlotKey, CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();

        var completedSlot = await session.Query<KnockoutSlot>()
            .FirstOrDefaultAsync(s => s.SlotKey == completedSlotKey, ct);

        if (completedSlot is null)
        {
            logger.LogWarning(
                "KnockoutBracketResolver: slot '{Key}' not found — cannot propagate",
                completedSlotKey);
            return;
        }

        // Ensure WinnerTeamId is set if we have enough information.
        var hadWinner = completedSlot.WinnerTeamId is not null;
        EnsureWinnerSet(completedSlot);

        if (completedSlot.WinnerTeamId is null)
        {
            logger.LogDebug(
                "KnockoutBracketResolver: slot '{Key}' has no winner yet — skipping propagation",
                completedSlotKey);
            return;
        }

        // Load only incomplete targets — these are the only slots PropagateSlotResult can mutate.
        var targetSlots = await session.Query<KnockoutSlot>()
            .Where(s => s.HomeTeamId == null || s.AwayTeamId == null)
            .ToListAsync(ct);

        var slotByKey = targetSlots.ToDictionary(s => s.SlotKey);
        slotByKey.TryAdd(completedSlot.SlotKey, completedSlot);

        var dirty = new HashSet<KnockoutSlot>();
        if (!hadWinner)
            dirty.Add(completedSlot);

        var changed = PropagateSlotResult(slotByKey, completedSlot, dirty);

        if (dirty.Count > 0)
        {
            foreach (var slot in dirty)
                session.Store(slot);

            await session.SaveChangesAsync(ct);

            if (changed > 0)
                logger.LogInformation(
                    "KnockoutBracketResolver: propagated result of '{Key}' into {Count} downstream slot(s)",
                    completedSlotKey, changed);
        }
    }

    /// <summary>
    /// Convenience overload that propagates all completed knockout slots in one pass.
    /// Useful after an admin override that sets multiple results at once.
    /// </summary>
    public async Task PropagateAllKnockoutResultsAsync(CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        if (await PropagateAllKnockoutResultsCoreAsync(session, ct) > 0)
            await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Shared-session overload — accumulates stores into <paramref name="session"/> without
    /// calling SaveChangesAsync. Caller is responsible for flushing.
    /// </summary>
    public async Task PropagateAllKnockoutResultsAsync(IDocumentSession session, CancellationToken ct = default)
        => await PropagateAllKnockoutResultsCoreAsync(session, ct);

    private async Task<int> PropagateAllKnockoutResultsCoreAsync(IDocumentSession session, CancellationToken ct)
    {
        // Load potential sources: slots with a recorded result or derivable winner.
        var sourceSlots = await session.Query<KnockoutSlot>()
            .Where(s => s.WinnerTeamId != null || s.HomeScore != null)
            .ToListAsync(ct);

        if (sourceSlots.Count == 0)
        {
            logger.LogDebug("KnockoutBracketResolver: no completed knockout slots — skipping propagation sweep");
            return 0;
        }

        // Load potential targets: slots still missing at least one team assignment.
        var targetSlots = await session.Query<KnockoutSlot>()
            .Where(s => s.HomeTeamId == null || s.AwayTeamId == null)
            .ToListAsync(ct);

        // Merge into one dict; a slot can be both a source and a target.
        var slotByKey = sourceSlots.ToDictionary(s => s.SlotKey);
        foreach (var t in targetSlots)
            slotByKey.TryAdd(t.SlotKey, t);

        var dirty = new HashSet<KnockoutSlot>();
        var totalChanged = 0;

        // Process in round order so upstream results flow downstream in one sweep.
        var orderedSlots = slotByKey.Values
            .OrderBy(s => (int)s.Round)
            .ThenBy(s => s.SlotNumber)
            .ToList();

        foreach (var slot in orderedSlots)
        {
            var hadWinner = slot.WinnerTeamId is not null;
            EnsureWinnerSet(slot);
            if (!hadWinner && slot.WinnerTeamId is not null)
                dirty.Add(slot);

            if (slot.WinnerTeamId is not null)
                totalChanged += PropagateSlotResult(slotByKey, slot, dirty);
        }

        if (dirty.Count > 0)
        {
            foreach (var slot in dirty)
                session.Store(slot);

            if (totalChanged > 0)
                logger.LogInformation(
                    "KnockoutBracketResolver: full propagation sweep changed {Count} slot team assignment(s)",
                    totalChanged);
        }

        return totalChanged;
    }

    // -------------------------------------------------------------------------
    // Internal pure logic — internal visibility for unit testing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Ranks all 12 groups and returns a dictionary keyed by group letter with a
    /// list of team IDs ordered 1st-4th according to FIFA criteria.
    /// </summary>
    internal static Dictionary<string, List<string>> RankAllGroups(
        IEnumerable<Group> groups,
        IEnumerable<Fixture> fixtures)
    {
        var fixturesByGroup = fixtures
            .GroupBy(f => f.GroupLetter)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<string, List<string>>();

        foreach (var group in groups)
        {
            var groupFixtures = fixturesByGroup.GetValueOrDefault(group.Letter, []);
            result[group.Letter] = RankGroup(group.TeamIds, groupFixtures);
        }

        return result;
    }

    /// <summary>
    /// Ranks teams within a single group.
    /// Returns team IDs in order: 1st, 2nd, 3rd, 4th.
    /// </summary>
    internal static List<string> RankGroup(
        IEnumerable<string> teamIds,
        IEnumerable<Fixture> fixtures)
    {
        var teams = teamIds.ToList();
        var fixtureList = fixtures
            .Where(f => f.HomeScore.HasValue && f.AwayScore.HasValue)
            .ToList();

        // Build per-team stats across all group games.
        var stats = teams.ToDictionary(t => t, _ => new TeamStats());

        foreach (var f in fixtureList)
        {
            var hs = f.HomeScore!.Value;
            var aws = f.AwayScore!.Value;

            if (!stats.ContainsKey(f.HomeTeamId) || !stats.ContainsKey(f.AwayTeamId))
                continue;

            stats[f.HomeTeamId].GoalsFor     += hs;
            stats[f.HomeTeamId].GoalsAgainst += aws;
            stats[f.AwayTeamId].GoalsFor     += aws;
            stats[f.AwayTeamId].GoalsAgainst += hs;

            if (hs > aws)
            {
                stats[f.HomeTeamId].Points += 3;
            }
            else if (hs == aws)
            {
                stats[f.HomeTeamId].Points += 1;
                stats[f.AwayTeamId].Points += 1;
            }
            else
            {
                stats[f.AwayTeamId].Points += 3;
            }
        }

        // Sort using full FIFA tiebreaker chain.
        return teams
            .OrderByDescending(t => stats[t].Points)
            .ThenByDescending(t => stats[t].GoalDifference)
            .ThenByDescending(t => stats[t].GoalsFor)
            .ThenByDescending(t => HeadToHeadPoints(t, teams, stats, fixtureList))
            .ThenByDescending(t => HeadToHeadGoalDifference(t, teams, stats, fixtureList))
            .ThenByDescending(t => HeadToHeadGoalsFor(t, teams, stats, fixtureList))
            .ToList();
    }

    /// <summary>
    /// Selects the 8 best third-placed teams, using fixture data to compute their
    /// overall stats for cross-group comparison.
    ///
    /// When <paramref name="incompleteGroups"/> is provided, a conservative upper-bound
    /// is computed for the eventual third-placed team from each incomplete group. The
    /// top-8 is only returned when all 8 positions are occupied by real (completed)
    /// teams — i.e., no incomplete group can possibly produce a better third-placed
    /// team than any of the current top-8. This allows early injection as soon as the
    /// result is mathematically certain without waiting for all 12 groups.
    /// </summary>
    internal static List<(string TeamId, string GroupLetter)> SelectBestThirdPlaced(
        Dictionary<string, List<string>> rankings,
        IEnumerable<Fixture> allFixtures,
        IEnumerable<Group>? incompleteGroups = null)
    {
        var allFixtureList = allFixtures.ToList();

        var completedFixtures = allFixtureList
            .Where(f => f.HomeScore.HasValue && f.AwayScore.HasValue)
            .ToList();

        var fixturesByGroup = completedFixtures
            .GroupBy(f => f.GroupLetter)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Collect third-placed teams with their overall stats from completed groups.
        var thirds = new List<(string? TeamId, string GroupLetter, int Points, int GD, int GF, bool IsReal)>();

        foreach (var kv in rankings)
        {
            if (kv.Value.Count < 3) continue;

            var teamId = kv.Value[2];
            var groupLetter = kv.Key;
            var groupFixtures = fixturesByGroup.GetValueOrDefault(groupLetter, []);

            var pts = 0;
            var gf = 0;
            var ga = 0;

            foreach (var f in groupFixtures)
            {
                if (f.HomeTeamId == teamId)
                {
                    gf += f.HomeScore!.Value;
                    ga += f.AwayScore!.Value;
                    if (f.HomeScore > f.AwayScore) pts += 3;
                    else if (f.HomeScore == f.AwayScore) pts += 1;
                }
                else if (f.AwayTeamId == teamId)
                {
                    gf += f.AwayScore!.Value;
                    ga += f.HomeScore!.Value;
                    if (f.AwayScore > f.HomeScore) pts += 3;
                    else if (f.AwayScore == f.HomeScore) pts += 1;
                }
            }

            thirds.Add((teamId, groupLetter, pts, gf - ga, gf, IsReal: true));
        }

        // For each incomplete group, compute a pessimistic upper-bound: the best a
        // third-placed team from that group could possibly achieve. This accounts for
        // partial results already played in the group.
        var incomplete = incompleteGroups?.ToList() ?? [];
        foreach (var group in incomplete)
        {
            var (maxPts, maxGD, maxGF) = ComputeMaxPossibleThirdStats(
                group.TeamIds, allFixtureList.Where(f => f.GroupLetter == group.Letter));
            thirds.Add((null, group.Letter, maxPts, maxGD, maxGF, IsReal: false));
        }

        // Sort all (real + virtual) candidates best-first.
        var sorted = thirds
            .OrderByDescending(t => t.Points)
            .ThenByDescending(t => t.GD)
            .ThenByDescending(t => t.GF)
            .ToList();

        // If the top 8 includes any virtual (incomplete-group) team, the final ranking
        // is not yet certain — an incomplete group could still produce a qualifying team.
        var top8 = sorted.Take(8).ToList();
        if (top8.Any(t => !t.IsReal))
            return [];

        return top8
            .Select(t => (t.TeamId!, t.GroupLetter))
            .ToList();
    }

    /// <summary>
    /// Computes a conservative upper bound on the stats that the eventual third-placed
    /// team from an incomplete group could achieve, given their remaining fixtures.
    /// Uses a practical per-game maximum GD of 8 goals — large enough to cover any
    /// realistic World Cup scoreline while still allowing early resolution once
    /// incomplete groups can no longer displace the locked top-8.
    /// </summary>
    internal static (int MaxPts, int MaxGD, int MaxGF) ComputeMaxPossibleThirdStats(
        IEnumerable<string> teamIds,
        IEnumerable<Fixture> groupFixtures,
        int maxGoalDiffPerGame = 8)
    {
        var teams = teamIds.ToList();
        if (teams.Count == 0) return (0, 0, 0);

        var fixtures = groupFixtures.ToList();
        var played   = fixtures.Where(f => f.HomeScore.HasValue && f.AwayScore.HasValue).ToList();
        var unplayed = fixtures.Where(f => !f.HomeScore.HasValue || !f.AwayScore.HasValue).ToList();

        // Accumulate partial standings.
        var pts = teams.ToDictionary(t => t, _ => 0);
        var gd  = teams.ToDictionary(t => t, _ => 0);
        var gf  = teams.ToDictionary(t => t, _ => 0);

        foreach (var f in played)
        {
            if (!pts.ContainsKey(f.HomeTeamId) || !pts.ContainsKey(f.AwayTeamId)) continue;
            var hs = f.HomeScore!.Value;
            var aws = f.AwayScore!.Value;
            gf[f.HomeTeamId] += hs;  gd[f.HomeTeamId] += hs - aws;
            gf[f.AwayTeamId] += aws; gd[f.AwayTeamId] += aws - hs;
            if      (hs > aws) pts[f.HomeTeamId] += 3;
            else if (hs < aws) pts[f.AwayTeamId] += 3;
            else { pts[f.HomeTeamId] += 1; pts[f.AwayTeamId] += 1; }
        }

        // Remaining games per team.
        var remaining = teams.ToDictionary(
            t => t,
            t => unplayed.Count(f => f.HomeTeamId == t || f.AwayTeamId == t));

        // Exclude teams definitively in the top 2: a team T is locked in top-2 when fewer
        // than 2 other teams can possibly earn more pts than T has right now.
        // (i.e., T can't be overtaken by ≥ 2 opponents even if they win every remaining game.)
        var definitivelyTop2 = teams
            .Where(t => teams.Count(s => s != t && (pts[s] + 3 * remaining[s]) > pts[t]) < 2)
            .ToHashSet();

        var thirdCandidates = teams.Where(t => !definitivelyTop2.Contains(t)).ToList();
        if (thirdCandidates.Count == 0) thirdCandidates = teams; // fallback: no group is fully resolved yet

        // Upper bound: a third-placed team has at most 6 points in a 4-team group.
        var maxPts = Math.Min(6, thirdCandidates.Max(t => pts[t] + 3 * remaining[t]));

        // Per-team GD/GF: the max each candidate can reach is their current stat plus
        // the buffer for their own remaining games (not the group's overall max remaining).
        var maxGD = thirdCandidates.Max(t => gd[t] + maxGoalDiffPerGame * remaining[t]);
        var maxGF = thirdCandidates.Max(t => gf[t] + maxGoalDiffPerGame * remaining[t]);

        return (maxPts, maxGD, maxGF);
    }

    /// <summary>
    /// Populates HomeTeamId/AwayTeamId on R32 slots (and any slot with a
    /// GroupWinner/GroupRunnerUp/BestThirdPlace source).
    /// Returns the number of slot-team assignments that changed.
    /// </summary>
    internal static int PopulateR32Slots(
        Dictionary<string, KnockoutSlot> slotByKey,
        Dictionary<string, List<string>> rankings,
        List<(string TeamId, string GroupLetter)> bestThirds)
    {
        var bestThirdByGroup = bestThirds.ToDictionary(t => t.GroupLetter, t => t.TeamId);

        var r32Slots = slotByKey.Values.Where(s => s.Round == Round.R32).ToList();

        // Pre-compute BestThirdPlace allocation via bipartite matching so that each
        // qualifying third-placed team is assigned to exactly one R32 slot.
        var btAllocation = AllocateBestThirds(bestThirdByGroup, r32Slots);

        var changed = 0;

        foreach (var slot in r32Slots)
        {
            var newHome = slot.HomeSlotSource.Type == SlotSourceType.BestThirdPlace
                ? btAllocation.GetValueOrDefault(slot.SlotKey)
                : ResolveGroupSource(slot.HomeSlotSource, rankings);

            var newAway = slot.AwaySlotSource.Type == SlotSourceType.BestThirdPlace
                ? btAllocation.GetValueOrDefault(slot.SlotKey)
                : ResolveGroupSource(slot.AwaySlotSource, rankings);

            if (newHome is not null && slot.HomeTeamId != newHome && !slot.HomeTeamOverridden)
            {
                slot.HomeTeamId = newHome;
                changed++;
            }

            if (newAway is not null && slot.AwayTeamId != newAway && !slot.AwayTeamOverridden)
            {
                slot.AwayTeamId = newAway;
                changed++;
            }
        }

        return changed;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves a single SlotSource that references a group position (GroupWinner or
    /// GroupRunnerUp).  Returns null for any other source type.
    /// BestThirdPlace sources are resolved upstream via <see cref="AllocateBestThirds"/>.
    /// </summary>
    private static string? ResolveGroupSource(
        SlotSource source,
        Dictionary<string, List<string>> rankings)
    {
        switch (source.Type)
        {
            case SlotSourceType.GroupWinner:
            {
                var letter = source.Reference.Trim();
                if (rankings.TryGetValue(letter, out var ranked) && ranked.Count >= 1)
                    return ranked[0];
                return null;
            }

            case SlotSourceType.GroupRunnerUp:
            {
                var letter = source.Reference.Trim();
                if (rankings.TryGetValue(letter, out var ranked) && ranked.Count >= 2)
                    return ranked[1];
                return null;
            }

            default:
                return null; // BestThirdPlace handled by AllocateBestThirds; MatchWinner/Loser elsewhere
        }
    }

    /// <summary>
    /// Assigns qualifying third-placed teams to BestThirdPlace R32 slots using a
    /// maximum bipartite matching (augmenting-path algorithm).
    ///
    /// Each slot's Reference encodes the groups whose third-placed team is eligible
    /// for that slot (e.g. "A/B/C/D/F"). The matching ensures each qualifying team
    /// is assigned to exactly one slot and each slot receives at most one team.
    ///
    /// Returns a dictionary keyed by SlotKey → TeamId (absent when no qualifier
    /// could be matched to that slot).
    /// </summary>
    private static Dictionary<string, string?> AllocateBestThirds(
        Dictionary<string, string> bestThirdByGroup,
        IEnumerable<KnockoutSlot> r32Slots)
    {
        // Collect at most one BestThirdPlace source per R32 slot.
        var btSources = r32Slots
            .Select(s => (s.SlotKey,
                Source: s.HomeSlotSource.Type == SlotSourceType.BestThirdPlace ? s.HomeSlotSource
                      : s.AwaySlotSource.Type == SlotSourceType.BestThirdPlace ? s.AwaySlotSource
                      : null))
            .Where(x => x.Source is not null)
            .ToList();

        // Eligible qualifying groups per slot.
        var eligiblePerSlot = btSources.ToDictionary(
            x => x.SlotKey,
            x => x.Source!.Reference
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(g => bestThirdByGroup.ContainsKey(g))
                .ToList());

        var slotToGroup = new Dictionary<string, string>();
        var groupToSlot = new Dictionary<string, string>();

        foreach (var slotKey in eligiblePerSlot.Keys)
        {
            var visited = new HashSet<string>();
            Augment(slotKey, eligiblePerSlot, slotToGroup, groupToSlot, visited);
        }

        return eligiblePerSlot.Keys.ToDictionary(
            slotKey => slotKey,
            slotKey => slotToGroup.TryGetValue(slotKey, out var g)
                ? bestThirdByGroup.GetValueOrDefault(g)
                : (string?)null);
    }

    /// <summary>DFS augmenting-path step for bipartite matching.</summary>
    private static bool Augment(
        string slotKey,
        Dictionary<string, List<string>> eligiblePerSlot,
        Dictionary<string, string> slotToGroup,
        Dictionary<string, string> groupToSlot,
        HashSet<string> visited)
    {
        foreach (var group in eligiblePerSlot[slotKey])
        {
            if (!visited.Add(group)) continue;

            if (!groupToSlot.TryGetValue(group, out var occupyingSlot) ||
                Augment(occupyingSlot, eligiblePerSlot, slotToGroup, groupToSlot, visited))
            {
                slotToGroup[slotKey] = group;
                groupToSlot[group] = slotKey;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Sets WinnerTeamId on a slot if it has scores but no winner yet.
    /// Only sets the winner when one side's score is strictly greater (regulation/ET
    /// winner).  Draws in 90 min where a winner was determined by penalties must have
    /// WinnerTeamId set externally by the ingestion pipeline.
    /// </summary>
    private static void EnsureWinnerSet(KnockoutSlot slot)
    {
        if (slot.WinnerTeamId is not null) return;
        if (slot.HomeScore is null || slot.AwayScore is null) return;
        if (slot.HomeTeamId is null || slot.AwayTeamId is null) return;

        if (slot.HomeScore > slot.AwayScore)
            slot.WinnerTeamId = slot.HomeTeamId;
        else if (slot.AwayScore > slot.HomeScore)
            slot.WinnerTeamId = slot.AwayTeamId;
        // Draw in 90 min — winner determined by penalties, set externally.
    }

    /// <summary>
    /// Given a completed slot, fills the downstream slot(s) that reference it.
    /// Returns the count of downstream slot assignments changed.
    /// </summary>
    private static int PropagateSlotResult(
        Dictionary<string, KnockoutSlot> slotByKey,
        KnockoutSlot completed,
        HashSet<KnockoutSlot>? dirty = null)
    {
        if (completed.WinnerTeamId is null || completed.HomeTeamId is null || completed.AwayTeamId is null)
            return 0;

        var loserTeamId = completed.WinnerTeamId == completed.HomeTeamId
            ? completed.AwayTeamId
            : completed.HomeTeamId;

        var changed = 0;

        foreach (var downstream in slotByKey.Values)
        {
            // Check Home slot source
            if (downstream.HomeSlotSource.Type == SlotSourceType.MatchWinner &&
                downstream.HomeSlotSource.Reference == completed.SlotKey &&
                !downstream.HomeTeamOverridden)
            {
                if (downstream.HomeTeamId != completed.WinnerTeamId)
                {
                    downstream.HomeTeamId = completed.WinnerTeamId;
                    dirty?.Add(downstream);
                    changed++;
                }
            }

            if (downstream.HomeSlotSource.Type == SlotSourceType.MatchLoser &&
                downstream.HomeSlotSource.Reference == completed.SlotKey &&
                !downstream.HomeTeamOverridden)
            {
                if (downstream.HomeTeamId != loserTeamId)
                {
                    downstream.HomeTeamId = loserTeamId;
                    dirty?.Add(downstream);
                    changed++;
                }
            }

            // Check Away slot source
            if (downstream.AwaySlotSource.Type == SlotSourceType.MatchWinner &&
                downstream.AwaySlotSource.Reference == completed.SlotKey &&
                !downstream.AwayTeamOverridden)
            {
                if (downstream.AwayTeamId != completed.WinnerTeamId)
                {
                    downstream.AwayTeamId = completed.WinnerTeamId;
                    dirty?.Add(downstream);
                    changed++;
                }
            }

            if (downstream.AwaySlotSource.Type == SlotSourceType.MatchLoser &&
                downstream.AwaySlotSource.Reference == completed.SlotKey &&
                !downstream.AwayTeamOverridden)
            {
                if (downstream.AwayTeamId != loserTeamId)
                {
                    downstream.AwayTeamId = loserTeamId;
                    dirty?.Add(downstream);
                    changed++;
                }
            }
        }

        return changed;
    }

    // -- FIFA tiebreaker helpers -----------------------------------------------

    /// <summary>Head-to-head points for a team against all teams it is tied with on overall points.</summary>
    private static int HeadToHeadPoints(
        string teamId,
        List<string> allTeams,
        Dictionary<string, TeamStats> overallStats,
        List<Fixture> fixtures)
    {
        var tiedTeams = GetTiedTeams(teamId, allTeams, overallStats);
        if (tiedTeams.Count <= 1) return 0; // no tie — tiebreaker not applied

        var pts = 0;
        foreach (var f in fixtures)
        {
            if (!IsH2HFixture(f, teamId, tiedTeams)) continue;

            if (f.HomeTeamId == teamId)
            {
                if (f.HomeScore > f.AwayScore) pts += 3;
                else if (f.HomeScore == f.AwayScore) pts += 1;
            }
            else
            {
                if (f.AwayScore > f.HomeScore) pts += 3;
                else if (f.AwayScore == f.HomeScore) pts += 1;
            }
        }

        return pts;
    }

    private static int HeadToHeadGoalDifference(
        string teamId,
        List<string> allTeams,
        Dictionary<string, TeamStats> overallStats,
        List<Fixture> fixtures)
    {
        var tiedTeams = GetTiedTeams(teamId, allTeams, overallStats);
        if (tiedTeams.Count <= 1) return 0;

        var gd = 0;
        foreach (var f in fixtures)
        {
            if (!IsH2HFixture(f, teamId, tiedTeams)) continue;

            if (f.HomeTeamId == teamId)
                gd += f.HomeScore!.Value - f.AwayScore!.Value;
            else
                gd += f.AwayScore!.Value - f.HomeScore!.Value;
        }

        return gd;
    }

    private static int HeadToHeadGoalsFor(
        string teamId,
        List<string> allTeams,
        Dictionary<string, TeamStats> overallStats,
        List<Fixture> fixtures)
    {
        var tiedTeams = GetTiedTeams(teamId, allTeams, overallStats);
        if (tiedTeams.Count <= 1) return 0;

        var gf = 0;
        foreach (var f in fixtures)
        {
            if (!IsH2HFixture(f, teamId, tiedTeams)) continue;

            if (f.HomeTeamId == teamId)
                gf += f.HomeScore!.Value;
            else
                gf += f.AwayScore!.Value;
        }

        return gf;
    }

    /// <summary>
    /// Returns all teams (including teamId itself) that are tied on overall points,
    /// GD, and GF with teamId.  A head-to-head tiebreaker applies only within the
    /// tied sub-group.
    /// </summary>
    private static List<string> GetTiedTeams(
        string teamId,
        List<string> allTeams,
        Dictionary<string, TeamStats> overallStats)
    {
        var s = overallStats[teamId];
        return allTeams
            .Where(t => overallStats[t].Points == s.Points
                        && overallStats[t].GoalDifference == s.GoalDifference
                        && overallStats[t].GoalsFor == s.GoalsFor)
            .ToList();
    }

    private static bool IsH2HFixture(Fixture f, string teamId, List<string> tiedTeams)
    {
        var involvesSelf = f.HomeTeamId == teamId || f.AwayTeamId == teamId;
        if (!involvesSelf) return false;

        var opponent = f.HomeTeamId == teamId ? f.AwayTeamId : f.HomeTeamId;
        return tiedTeams.Contains(opponent);
    }
}

/// <summary>Mutable per-team stats accumulator used during group ranking.</summary>
internal sealed class TeamStats
{
    public int Points       { get; set; }
    public int GoalsFor     { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
