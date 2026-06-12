using Marten;
using Quartz;
using TriviumWorldCup.Api.Admin;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Knockout;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Quartz job that polls API-Football for completed group-stage fixtures, records scores
/// and goal events in Marten, then triggers a scoring recompute.
///
/// Scheduling strategy (single-trigger, adaptive):
///   - Job runs every 30 seconds unconditionally.
///   - At the start of each execution, the job checks whether any fixture is in a
///     "live window" (KickoffUtc within the last 3 hours OR in the next 30 minutes).
///   - If no live window is detected, the job performs a lightweight check for any
///     new FT fixtures since the last run and exits early to respect rate limits.
///   - During live windows the full ingestion pipeline runs every cycle.
///   - API-Football free plan allows 100 requests/day; each full cycle costs at most
///     1 (GetAll) + N (events per newly-completed fixture) requests. With 72 fixtures
///     and worst-case 72 event calls = 73 calls in one burst; subsequent runs only
///     call events for fixtures newly completed since last poll.
///
/// Idempotency:
///   - Already-Completed fixtures in Marten are skipped (no API call for events).
///   - GoalEvent IDs are deterministic: Version 5 UUID derived from
///     (fixtureId, playerName, minute) — re-processing the same match produces the
///     same Guid, so session.Store() is an upsert that overwrites with identical data.
///
/// Player matching:
///   - Goal scorer resolved by exact name match against Player Marten documents.
///   - Unmatched names are silently skipped (no exception); the fixture score is still
///     recorded correctly. These can be reconciled manually via a future admin endpoint.
/// </summary>
[DisallowConcurrentExecution]
public class ResultIngestionJob(
    IFootballApiClient apiClient,
    IDocumentStore store,
    ScoringRecomputeService scoringService,
    KnockoutBracketResolver bracketResolver,
    IngestionStatusStore statusStore,
    ILogger<ResultIngestionJob> logger) : IJob
{
    // Version 5 UUID namespace for deterministic GoalEvent IDs.
    // Using a fixed, arbitrary namespace GUID registered for this purpose.
    private static readonly Guid GoalEventNamespace = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        logger.LogDebug("ResultIngestionJob: starting poll");

        // ── 0. Update status store: record attempt ────────────────────────────
        statusStore.LastAttemptedPoll = DateTimeOffset.UtcNow;
        statusStore.TotalPollCount++;

        // ── 1. Check local DB for live window — no API call unless a match is on ─
        // The fixture seed data has all kickoff times, so we can determine the live
        // window entirely from our own DB without spending a Football API request.
        var now = DateTimeOffset.UtcNow;
        var liveWindowStart = now.AddHours(-3);
        var liveWindowEnd   = now.AddMinutes(30);

        await using var checkSession = store.LightweightSession();
        var anyLiveInDb = await checkSession
            .Query<Fixture>()
            .Where(f => f.Status != MatchStatus.Completed
                     && f.Status != MatchStatus.Cancelled
                     && f.KickoffUtc >= liveWindowStart
                     && f.KickoffUtc <= liveWindowEnd)
            .AnyAsync(ct);

        // Also check knockout slots — during the knockout phase all group Fixtures are
        // Completed so the query above returns false even when a knockout match is live.
        if (!anyLiveInDb)
        {
            anyLiveInDb = await checkSession
                .Query<KnockoutSlot>()
                .Where(s => s.Status != MatchStatus.Completed
                         && s.Status != MatchStatus.Cancelled
                         && s.KickoffUtc != null
                         && s.KickoffUtc >= liveWindowStart
                         && s.KickoffUtc <= liveWindowEnd)
                .AnyAsync(ct);
        }

        if (!anyLiveInDb)
        {
            logger.LogDebug("ResultIngestionJob: no fixtures in live window — skipping API call");
            return;
        }

        // ── 2. Fetch fixtures for the relevant UTC date(s) from API ──────────
        // Fetch by date instead of the full season to keep each call cheap (max 5–6
        // fixtures returned). The live window can span two UTC dates when a match
        // kicks off close to midnight UTC, so we fetch both dates in that case.
        IReadOnlyList<ApiFixture> allApiFixtures;
        try
        {
            var today          = DateOnly.FromDateTime(now.UtcDateTime);
            var windowStartDay = DateOnly.FromDateTime(liveWindowStart.UtcDateTime);
            allApiFixtures = await apiClient.GetFixturesByDateAsync(today, ct);
            if (windowStartDay != today)
            {
                var prev = await apiClient.GetFixturesByDateAsync(windowStartDay, ct);
                allApiFixtures = [..allApiFixtures, ..prev];
            }
        }
        catch (Exception ex)
        {
            statusStore.LastError = ex.Message;
            statusStore.ErrorCount++;
            logger.LogWarning(ex, "ResultIngestionJob: failed to fetch fixtures from API — will retry on next cycle");
            return;
        }

        if (allApiFixtures.Count == 0)
        {
            logger.LogDebug("ResultIngestionJob: no fixtures returned from API");
            return;
        }

        var anyLive = allApiFixtures.Any(f => f.IsLive);

        // ── 3. Load already-completed fixtures from Marten ───────────────────
        await using var session = store.LightweightSession();

        var completedInDb = await session
            .Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Completed)
            .Select(f => f.Id)
            .ToListAsync(ct);

        var completedSet = new HashSet<string>(completedInDb);

        // ── 4. Find API fixtures that are FT but not yet Completed in Marten ─
        var toIngest = allApiFixtures
            .Where(af => af.IsFullTime)
            .Where(af =>
            {
                // Resolve FIFA codes to find the matching Marten Fixture.Id
                var homeCode = FootballApiTeamMap.Resolve(af.HomeTeamId, af.HomeTeamName);
                var awayCode = FootballApiTeamMap.Resolve(af.AwayTeamId, af.AwayTeamName);
                if (homeCode == null || awayCode == null) return false;

                // We need to find the Fixture by team pair — we'll do the full resolution below.
                return true;
            })
            .ToList();

        if (!anyLive && toIngest.Count == 0)
        {
            logger.LogDebug("ResultIngestionJob: no live window, no new completed fixtures — skipping");
            return;
        }

        // ── 5. Load all group fixtures from Marten for matching ───────────────
        var allDbFixtures = await session
            .Query<Fixture>()
            .ToListAsync(ct);

        // Index Marten fixtures by team pair for O(1) lookup
        var fixtureByTeamPair = allDbFixtures
            .ToDictionary(f => (f.HomeTeamId, f.AwayTeamId));

        // ── 6. Pre-load all players for name-based matching ───────────────────
        var allPlayers = await session
            .Query<Player>()
            .ToListAsync(ct);

        var playerByName = allPlayers
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ── 7. Process each newly-completed fixture ───────────────────────────
        var ingestedCount = 0;

        foreach (var apiFixture in allApiFixtures.Where(f => f.IsFullTime))
        {
            var homeCode = FootballApiTeamMap.Resolve(apiFixture.HomeTeamId, apiFixture.HomeTeamName);
            var awayCode = FootballApiTeamMap.Resolve(apiFixture.AwayTeamId, apiFixture.AwayTeamName);

            if (homeCode == null || awayCode == null)
            {
                logger.LogWarning(
                    "ResultIngestionJob: cannot resolve FIFA codes for API fixture {FixtureId} " +
                    "({Home} vs {Away}) — skipping",
                    apiFixture.FixtureId, apiFixture.HomeTeamName, apiFixture.AwayTeamName);
                continue;
            }

            if (!fixtureByTeamPair.TryGetValue((homeCode, awayCode), out var dbFixture))
            {
                logger.LogWarning(
                    "ResultIngestionJob: no Marten fixture found for {Home} vs {Away} — skipping",
                    homeCode, awayCode);
                continue;
            }

            // Skip if already completed AND events were successfully ingested.
            // If Completed but EventsIngested=false, retry events this cycle (backfill after quota reset / transient error).
            if (completedSet.Contains(dbFixture.Id) && dbFixture.EventsIngested)
            {
                logger.LogDebug(
                    "ResultIngestionJob: fixture {Id} already Completed with events ingested — skipping",
                    dbFixture.Id);
                continue;
            }

            // Update fixture scores, status, and API fixture ID
            dbFixture.HomeScore            = apiFixture.HomeGoals;
            dbFixture.AwayScore            = apiFixture.AwayGoals;
            dbFixture.Status               = MatchStatus.Completed;
            dbFixture.ElapsedMinute        = null;
            dbFixture.ElapsedExtra         = null;
            dbFixture.FootballApiFixtureId ??= apiFixture.FixtureId;
            session.Store(dbFixture);

            // Fetch all events in one request (goals, cards, subs, VAR) then split locally.
            // Saves 3 API requests per completed fixture vs. calling /fixtures/events four times
            // with separate type= filters.
            IReadOnlyList<ApiMatchEvent> allEvents = [];
            var eventsIngestionFailed = false;
            var eventsIngestionWasQuotaError = false;

            if (apiFixture.IsFullTime)
            {
                try
                {
                    allEvents = await apiClient.GetAllEventsAsync(apiFixture.FixtureId, ct);
                }
                catch (HttpRequestException ex) when (ex.InnerException is InvalidOperationException { Message: "Quota exceeded" })
                {
                    eventsIngestionFailed = true;
                    eventsIngestionWasQuotaError = true;
                    logger.LogWarning(ex,
                        "ResultIngestionJob: API-Football quota exhausted while fetching events for fixture {FixtureId}. " +
                        "Score recorded; events will backfill after quota reset (00:00 UTC).",
                        apiFixture.FixtureId);
                    statusStore.LastError = "API quota exhausted (429)";
                }
                catch (Exception ex)
                {
                    eventsIngestionFailed = true;
                    logger.LogWarning(ex,
                        "ResultIngestionJob: transient error fetching events for fixture {FixtureId} — " +
                        "score recorded but events skipped, will retry on next cycle",
                        apiFixture.FixtureId);
                }
            }

            var goalEvents = allEvents.Where(e => e.IsGoal).ToList();
            var varEvents  = allEvents.Where(e => e.IsVar).ToList();
            var cardEvents = allEvents.Where(e => e.IsCard).ToList();
            var subEvents  = allEvents.Where(e => e.IsSub).ToList();

            // Build lookups: cancelled goals by (playerName, minute) and card-upgrade decisions.
            // Keying on minute prevents a valid earlier goal by the same player being discarded.
            var cancelledGoals = varEvents
                .Where(v => v.IsGoalCancelled && v.Player?.Name is { Length: > 0 })
                .Select(v => (Name: v.Player!.Name!.Trim().ToUpperInvariant(), Minute: v.Time?.Elapsed ?? 0))
                .ToHashSet();

            var cardUpgrades = varEvents
                .Where(v => (v.IsCardUpgradeRed || v.IsCardUpgrade2ndYel) && v.Player?.Name is { Length: > 0 })
                .ToDictionary(
                    v => v.Player!.Name!,
                    v => v.IsCardUpgradeRed ? CardType.Red : CardType.SecondYellow,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var evt in goalEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName)
                    continue;

                // Skip goals cancelled by VAR — match on both player name and minute so a valid
                // earlier goal by the same player is not discarded.
                if (cancelledGoals.Contains((playerName.Trim().ToUpperInvariant(), evt.Time?.Elapsed ?? 0)))
                {
                    logger.LogDebug(
                        "ResultIngestionJob: goal by '{Player}' at {Minute}' cancelled by VAR — skipping",
                        playerName, evt.Time?.Elapsed ?? 0);
                    continue;
                }

                // Resolve player by name
                if (!playerByName.TryGetValue(playerName, out var player))
                {
                    logger.LogDebug(
                        "ResultIngestionJob: player '{Name}' not found in roster — goal event skipped",
                        playerName);
                    continue;
                }

                var goalType = evt.IsOwnGoal     ? GoalType.OwnGoal :
                               evt.IsPenalty     ? GoalType.PenaltyInMatch :
                                                   GoalType.OpenPlay;

                // Deterministic ID: same fixture + player name + minute → same Guid
                var goalId = CreateDeterministicGuid(GoalEventNamespace,
                    $"{apiFixture.FixtureId}:{playerName}:{evt.Time?.Elapsed ?? 0}");

                var goalEvent = new GoalEvent
                {
                    Id          = goalId,
                    FixtureId   = dbFixture.Id,
                    PlayerId    = player.Id,
                    Type        = goalType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                };

                session.Store(goalEvent);
            }

            foreach (var evt in cardEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName)
                    continue;

                // Skip unknown card types
                if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed)
                    continue;

                if (!playerByName.TryGetValue(playerName, out var player))
                {
                    logger.LogDebug(
                        "ResultIngestionJob: player '{Name}' not found in roster — card event skipped",
                        playerName);
                    continue;
                }

                var cardType = evt.IsSecondYellow ? CardType.SecondYellow :
                               evt.IsRed          ? CardType.Red :
                                                    CardType.Yellow;

                // Apply VAR card upgrade if one exists for this player
                if (cardUpgrades.TryGetValue(playerName, out var upgradedType))
                {
                    logger.LogDebug(
                        "ResultIngestionJob: card for '{Player}' upgraded by VAR from {Original} to {Upgraded}",
                        playerName, cardType, upgradedType);
                    cardType = upgradedType;
                }

                var cardId = CreateDeterministicGuid(GoalEventNamespace,
                    $"card:{apiFixture.FixtureId}:{playerName}:{cardType}:{evt.Time?.Elapsed ?? 0}");

                session.Store(new CardEvent
                {
                    Id          = cardId,
                    FixtureId   = dbFixture.Id,
                    PlayerId    = player.Id,
                    Type        = cardType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
            }

            foreach (var evt in subEvents)
            {
                var playerOutName = evt.Player?.Name ?? string.Empty;
                var playerInName  = evt.Assist?.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(playerOutName) && string.IsNullOrWhiteSpace(playerInName))
                    continue;

                playerByName.TryGetValue(playerOutName, out var playerOut);
                playerByName.TryGetValue(playerInName,  out var playerIn);

                // Resolve team FIFA code from API team id
                var teamFifaCode = evt.Team?.Name is { } teamName
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, teamName) ?? string.Empty
                    : string.Empty;

                var subKey = $"sub:{apiFixture.FixtureId}:{playerOutName}:{playerInName}:{evt.Time?.Elapsed ?? 0}";
                var subId  = CreateDeterministicGuid(GoalEventNamespace, subKey);

                session.Store(new SubstitutionEvent
                {
                    Id            = subId,
                    FixtureId     = dbFixture.Id,
                    PlayerOutId   = playerOut?.Id,
                    PlayerInId    = playerIn?.Id,
                    PlayerOutName = playerOutName,
                    PlayerInName  = playerInName,
                    TeamId        = teamFifaCode,
                    Minute        = evt.Time?.Elapsed ?? 0,
                    ExtraMinute   = evt.Time?.Extra,
                });
            }

            if (!eventsIngestionFailed)
            {
                dbFixture.EventsIngested = true;
            }

            ingestedCount++;
            logger.LogInformation(
                "ResultIngestionJob: ingested fixture {Id} ({Home} {HomeScore}-{AwayScore} {Away}), {Goals} goal(s), {Subs} sub(s), events={EventsStatus}",
                dbFixture.Id, homeCode, apiFixture.HomeGoals, apiFixture.AwayGoals, awayCode,
                goalEvents.Count(e => e.Player?.Name != null), subEvents.Count,
                eventsIngestionFailed ? (eventsIngestionWasQuotaError ? "429_quota" : "failed") : "ok");
        }

        // ── 8a. Update in-progress group-stage fixture clocks ───────────────────
        // Keeps Status=InProgress and ElapsedMinute current while a match is live.
        foreach (var apiFixture in allApiFixtures.Where(f => f.IsLive))
        {
            var liveHomeCode = FootballApiTeamMap.Resolve(apiFixture.HomeTeamId, apiFixture.HomeTeamName);
            var liveAwayCode = FootballApiTeamMap.Resolve(apiFixture.AwayTeamId, apiFixture.AwayTeamName);
            if (liveHomeCode == null || liveAwayCode == null) continue;
            if (!fixtureByTeamPair.TryGetValue((liveHomeCode, liveAwayCode), out var liveDbFixture)) continue;
            if (completedSet.Contains(liveDbFixture.Id)) continue;

            liveDbFixture.Status               = MatchStatus.InProgress;
            liveDbFixture.HomeScore            = apiFixture.HomeGoals;
            liveDbFixture.AwayScore            = apiFixture.AwayGoals;
            liveDbFixture.ElapsedMinute        = apiFixture.StatusElapsed;
            liveDbFixture.ElapsedExtra         = apiFixture.StatusExtra;
            liveDbFixture.FootballApiFixtureId ??= apiFixture.FixtureId;
            session.Store(liveDbFixture);
        }

        // ── 8. Process live and completed knockout fixtures ───────────────────────
        // The API call above returns all fixtures for the tournament, so knockout
        // fixtures are already in allApiFixtures. We match them to KnockoutSlot
        // documents by team pair and update scores / status in real-time.
        var knockoutSlots = await session
            .Query<KnockoutSlot>()
            .Where(s => s.HomeTeamId != null
                     && s.AwayTeamId != null
                     && s.Status != MatchStatus.Completed
                     && s.Status != MatchStatus.Cancelled)
            .ToListAsync(ct);

        var knockoutUpdated = 0;
        if (knockoutSlots.Count > 0)
        {
            var knockoutByTeamPair = knockoutSlots
                .ToDictionary(s => (s.HomeTeamId!, s.AwayTeamId!));

            foreach (var apiFixture in allApiFixtures.Where(f => f.IsLive || f.IsFullTime))
            {
                var homeCode = FootballApiTeamMap.Resolve(apiFixture.HomeTeamId, apiFixture.HomeTeamName);
                var awayCode = FootballApiTeamMap.Resolve(apiFixture.AwayTeamId, apiFixture.AwayTeamName);
                if (homeCode == null || awayCode == null) continue;
                if (!knockoutByTeamPair.TryGetValue((homeCode, awayCode), out var slot)) continue;

                if (apiFixture.IsLive)
                {
                    slot.Status = apiFixture.StatusShort is "ET" or "BT"
                                ? MatchStatus.ExtraTime
                                : apiFixture.StatusShort is "P"
                                ? MatchStatus.PenaltyShootout
                                : MatchStatus.InProgress;
                    slot.HomeScore = apiFixture.HomeGoals;
                    slot.AwayScore = apiFixture.AwayGoals;
                    if (slot.Status == MatchStatus.PenaltyShootout)
                    {
                        slot.PenaltyHomeScore = apiFixture.ScorePenaltyHome;
                        slot.PenaltyAwayScore = apiFixture.ScorePenaltyAway;
                    }
                }
                else // IsFullTime
                {
                    // Store the 90-minute score (score.fulltime) — this is what the prediction
                    // scoring system compares against. Falls back to goals total if not available.
                    slot.HomeScore = apiFixture.ScoreFullTimeHome ?? apiFixture.HomeGoals;
                    slot.AwayScore = apiFixture.ScoreFullTimeAway ?? apiFixture.AwayGoals;
                    slot.Status    = MatchStatus.Completed;

                    // Determine winner
                    if (slot.HomeScore > slot.AwayScore)
                        slot.WinnerTeamId = slot.HomeTeamId;
                    else if (slot.AwayScore > slot.HomeScore)
                        slot.WinnerTeamId = slot.AwayTeamId;
                    else if (apiFixture.StatusShort is "AET")
                    {
                        // ET tipped the balance — HomeGoals/AwayGoals hold the AET total
                        if (apiFixture.HomeGoals > apiFixture.AwayGoals)
                            slot.WinnerTeamId = slot.HomeTeamId;
                        else if (apiFixture.AwayGoals > apiFixture.HomeGoals)
                            slot.WinnerTeamId = slot.AwayTeamId;
                    }
                    else if (apiFixture.StatusShort is "PEN")
                    {
                        slot.PenaltyHomeScore = apiFixture.ScorePenaltyHome;
                        slot.PenaltyAwayScore = apiFixture.ScorePenaltyAway;
                        if (slot.PenaltyHomeScore > slot.PenaltyAwayScore)
                            slot.WinnerTeamId = slot.HomeTeamId;
                        else if (slot.PenaltyAwayScore > slot.PenaltyHomeScore)
                            slot.WinnerTeamId = slot.AwayTeamId;
                    }

                    logger.LogInformation(
                        "ResultIngestionJob: knockout slot {SlotKey} completed — {Home} {HomeScore}-{AwayScore} {Away}, winner={Winner}",
                        slot.SlotKey, homeCode, slot.HomeScore, slot.AwayScore, awayCode,
                        slot.WinnerTeamId ?? "TBD");
                }

                session.Store(slot);
                knockoutUpdated++;
            }
        }

        if (ingestedCount > 0 || anyLive || knockoutUpdated > 0)
        {
            await session.SaveChangesAsync(ct);
        }

        if (ingestedCount > 0 || knockoutUpdated > 0)
        {
            logger.LogInformation(
                "ResultIngestionJob: {GroupCount} group fixture(s), {KnockoutCount} knockout slot(s) updated — triggering bracket resolution and score recompute",
                ingestedCount, knockoutUpdated);
            await bracketResolver.ResolveGroupStageAsync(ct);
            await bracketResolver.PropagateAllKnockoutResultsAsync(ct);
            await scoringService.RecomputeAllAsync(ct);
        }

        // ── Record successful poll ────────────────────────────────────────────
        statusStore.LastSuccessfulPoll = DateTimeOffset.UtcNow;
        statusStore.LastError = null;

        logger.LogDebug("ResultIngestionJob: completed (ingested={Count}, liveWindow={Live})",
            ingestedCount, anyLive);
    }

    /// <summary>
    /// Creates a deterministic Version 5 UUID (SHA-1 based) from a namespace GUID and a name string.
    /// Re-running with the same inputs always produces the same output — this is the foundation
    /// for idempotent GoalEvent storage: session.Store() is a safe upsert.
    /// </summary>
    internal static Guid CreateDeterministicGuid(Guid namespaceId, string name)
    {
        // RFC 4122 §4.3 — UUID v5 algorithm
        var nsBytes  = namespaceId.ToByteArray();
        // Convert namespace from .NET mixed-endian to big-endian network byte order
        SwapEndian(nsBytes);

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);

        byte[] hash;
        using (var sha = System.Security.Cryptography.SHA1.Create())
        {
            sha.TransformBlock(nsBytes, 0, nsBytes.Length, null, 0);
            sha.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
            hash = sha.Hash!;
        }

        // Set version (5) and variant (RFC 4122)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // version 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant RFC 4122

        // Convert first 16 bytes back to .NET mixed-endian Guid
        var guidBytes = hash[..16];
        SwapEndian(guidBytes);
        return new Guid(guidBytes);
    }

    private static void SwapEndian(byte[] b)
    {
        // .NET Guid stores first three components in little-endian; RFC 4122 uses big-endian
        Array.Reverse(b, 0, 4);
        Array.Reverse(b, 4, 2);
        Array.Reverse(b, 6, 2);
    }

    /// <summary>
    /// Maps an API-Football goal event detail string to the app's <see cref="GoalType"/>.
    /// Pure function — testable without any infrastructure.
    /// </summary>
    internal static GoalType MapGoalType(ApiMatchEvent evt)
    {
        if (evt.IsOwnGoal) return GoalType.OwnGoal;
        if (evt.IsPenalty) return GoalType.PenaltyInMatch;
        return GoalType.OpenPlay;
    }

    /// <summary>
    /// Constructs a <see cref="GoalEvent"/> with a deterministic ID.
    /// Pure function — no database or API calls.
    /// </summary>
    internal static GoalEvent BuildGoalEvent(
        int apiFixtureId,
        string dbFixtureId,
        Guid playerId,
        ApiMatchEvent evt)
    {
        var playerName = evt.Player?.Name ?? string.Empty;
        var minute     = evt.Time?.Elapsed ?? 0;

        return new GoalEvent
        {
            Id          = CreateDeterministicGuid(GoalEventNamespace,
                              $"{apiFixtureId}:{playerName}:{minute}"),
            FixtureId   = dbFixtureId,
            PlayerId    = playerId,
            Type        = MapGoalType(evt),
            Minute      = minute,
            ExtraMinute = evt.Time?.Extra,
        };
    }
}
