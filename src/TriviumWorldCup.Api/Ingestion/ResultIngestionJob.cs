using Marten;
using Microsoft.AspNetCore.OutputCaching;
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
///   - At the start of each execution, the job checks the LOCAL DATABASE only (no API
///     call) for a "live window": a fixture/slot is in it if it's already InProgress (or
///     ExtraTime/PenaltyShootout for knockout slots) — regardless of how long ago kickoff
///     was, since matches run well past 30 minutes — OR it's still Scheduled with
///     KickoffUtc within 30 minutes either side of now, to catch an imminent kickoff.
///   - If nothing is in that window, the job returns immediately — zero API calls.
///   - Only when the DB check finds a candidate does the job call the Football API.
///   - Fixtures/slots the API reports as postponed or cancelled are marked Postponed or
///     Cancelled (respectively) in Marten immediately, so the DB-only check above — which
///     only opens the live window for Scheduled fixtures — stops doing so for them on
///     future polls.
///
/// Idempotency:
///   - Already-Completed fixtures in Marten are skipped (no API call for events).
///   - GoalEvent/CardEvent/SubstitutionEvent IDs are deterministic: Version 5 UUID derived
///     from (fixtureId, resolved PlayerId, minute) — re-processing the same match produces
///     the same Guid, so session.Store() is an upsert that overwrites with identical data.
///     The resolved PlayerId is used rather than the raw API name text because API-Football
///     doesn't always return the same name format for a player across separate calls.
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
    PlayerCache playerCache,
    IOutputCacheStore outputCache,
    ILogger<ResultIngestionJob> logger) : IJob
{
    // Version 5 UUID namespace for deterministic GoalEvent IDs.
    // Using a fixed, arbitrary namespace GUID registered for this purpose.
    private static readonly Guid GoalEventNamespace = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    // Minimum gap between consecutive RecomputeAllAsync calls. Guards against the
    // simultaneous-group-match scenario: when two final-round fixtures complete within the
    // same ~30-second window, the API may report them FT in successive poll cycles, which
    // would otherwise trigger two back-to-back full recomputes. Skipped recomputes self-heal
    // on the next 30s poll cycle (ingestedCount > 0 still triggers recompute). Do not raise
    // the job cadence past 30s without revisiting this assumption.
    private static readonly TimeSpan RecomputeMinInterval = TimeSpan.FromSeconds(20);

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
        var liveWindowStart = now.AddMinutes(-30);
        var liveWindowEnd   = now.AddMinutes(30);

        // The ±30min window only gates fixtures that haven't kicked off yet (Scheduled) —
        // it catches an upcoming kickoff cheaply. Once a fixture/slot is confirmed
        // InProgress (or in extra time / penalties for knockout slots), it's polled every
        // cycle regardless of elapsed time, since matches can run well past 30 minutes
        // (stoppage time, extra time, penalty shootouts) and we must not stop tracking
        // a match that's actually still being played.
        await using var checkSession = store.LightweightSession();
        var anyLiveInDb = await checkSession
            .Query<Fixture>()
            .Where(f => f.Status == MatchStatus.InProgress
                     || (f.Status == MatchStatus.Scheduled
                         && f.KickoffUtc >= liveWindowStart
                         && f.KickoffUtc <= liveWindowEnd))
            .AnyAsync(ct);

        // Also check knockout slots — during the knockout phase all group Fixtures are
        // Completed so the query above returns false even when a knockout match is live.
        if (!anyLiveInDb)
        {
            anyLiveInDb = await checkSession
                .Query<KnockoutSlot>()
                .Where(s => s.Status == MatchStatus.InProgress
                         || s.Status == MatchStatus.ExtraTime
                         || s.Status == MatchStatus.PenaltyShootout
                         || (s.Status == MatchStatus.Scheduled
                             && s.KickoffUtc != null
                             && s.KickoffUtc >= liveWindowStart
                             && s.KickoffUtc <= liveWindowEnd))
                .AnyAsync(ct);
        }

        // ── 1b. Periodically recheck Postponed fixtures/slots for a status change ─
        // A postponed fixture sits on its ORIGINAL kickoff date, which the per-date
        // fetch in step 2 won't include once that date is no longer "today" — so without
        // this, a postponed fixture would never be looked at again. Throttled to once a
        // minute (rather than every 30s) since a new kickoff time, cancellation, or
        // resumption is not time-critical the way an in-progress match is.
        var shouldRecheckPostponed = false;
        if (statusStore.LastPostponedRecheck is null
            || now - statusStore.LastPostponedRecheck >= TimeSpan.FromMinutes(1))
        {
            shouldRecheckPostponed = await checkSession.Query<Fixture>()
                .Where(f => f.Status == MatchStatus.Postponed)
                .AnyAsync(ct);

            if (!shouldRecheckPostponed)
            {
                shouldRecheckPostponed = await checkSession.Query<KnockoutSlot>()
                    .Where(s => s.Status == MatchStatus.Postponed)
                    .AnyAsync(ct);
            }
        }

        if (!anyLiveInDb && !shouldRecheckPostponed)
        {
            logger.LogDebug("ResultIngestionJob: no fixtures in live window — skipping API call");
            return;
        }

        if (shouldRecheckPostponed)
        {
            statusStore.LastPostponedRecheck = now;
            await RecheckPostponedFixturesAsync(ct);
        }

        if (!anyLiveInDb)
        {
            // Nothing else due this cycle — the postponed recheck above already ran.
            return;
        }

        // ── 2. Fetch fixtures for the relevant UTC date(s) from API ──────────
        // Fetch by date instead of the full season to keep each call cheap (max 5–6
        // fixtures returned). The live window can span two UTC dates when a match
        // kicks off close to midnight UTC, so we fetch both dates in that case.
        var today          = DateOnly.FromDateTime(now.UtcDateTime);
        var windowStartDay = DateOnly.FromDateTime(liveWindowStart.UtcDateTime);
        IReadOnlyList<ApiFixture> allApiFixtures;
        try
        {
            allApiFixtures = await apiClient.GetFixturesByDateAsync(today, ct);
            if (windowStartDay != today)
            {
                var prev = await apiClient.GetFixturesByDateAsync(windowStartDay, ct);
                allApiFixtures = allApiFixtures.Concat(prev).DistinctBy(f => f.FixtureId).ToList();
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

        // ── 2b. Catch-up fetch for InProgress fixtures that crossed a UTC date boundary ──
        // The main fetch above covers today and at most yesterday (within the first 30 minutes
        // after midnight UTC). A match that kicked off around 23:00–23:30 UTC can still be
        // live past 00:30 UTC, at which point its date is no longer fetched and the FT signal
        // is never seen — leaving the fixture permanently stuck at InProgress with a frozen
        // ElapsedMinute. Detect any InProgress fixture/slot whose kickoff date isn't covered
        // and fetch that date once to recover the FT (or continued-live) signal.
        var alreadyFetched = new HashSet<DateOnly> { today };
        if (windowStartDay != today) alreadyFetched.Add(windowStartDay);

        var groupKickoffs = await checkSession
            .Query<Fixture>()
            .Where(f => f.Status == MatchStatus.InProgress)
            .Select(f => f.KickoffUtc)
            .ToListAsync(ct);

        var knockoutKickoffs = await checkSession
            .Query<KnockoutSlot>()
            .Where(s => s.KickoffUtc != null
                     && (s.Status == MatchStatus.InProgress
                      || s.Status == MatchStatus.ExtraTime
                      || s.Status == MatchStatus.PenaltyShootout))
            .Select(s => s.KickoffUtc!.Value)
            .ToListAsync(ct);

        var missedDates = groupKickoffs
            .Select(k => DateOnly.FromDateTime(k.UtcDateTime))
            .Concat(knockoutKickoffs.Select(k => DateOnly.FromDateTime(k.UtcDateTime)))
            .Where(alreadyFetched.Add)
            .ToList();

        foreach (var date in missedDates)
        {
            try
            {
                var extra = await apiClient.GetFixturesByDateAsync(date, ct);
                allApiFixtures = [..allApiFixtures.Concat(extra).DistinctBy(f => f.FixtureId)];
                logger.LogInformation(
                    "ResultIngestionJob: catch-up fetch for {Date} — {Count} fixture(s) recovered",
                    date, extra.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ResultIngestionJob: catch-up fetch for {Date} failed", date);
            }
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

        // ── 4. Check whether there are FT fixtures or cancellations to process ─
        // toIngest was previously computed here but was functionally just "any FT fixture" —
        // the actual per-fixture team-pair resolution happens in step 7. Inline the check.
        var anyFtFixtures           = allApiFixtures.Any(af => af.IsFullTime);
        var anyCancelledOrPostponed = allApiFixtures.Any(f => f.IsCancelledOrPostponed);

        if (!anyLive && !anyFtFixtures && !anyCancelledOrPostponed)
        {
            logger.LogDebug("ResultIngestionJob: no live window, no new completed fixtures — skipping");
            return;
        }

        // ── 5. Load all group fixtures from Marten for matching ───────────────
        var allDbFixtures = await session
            .Query<Fixture>()
            .ToListAsync(ct);

        // Index Marten fixtures by team pair for O(1) lookup.
        // Safe only because group-stage team pairs are unique (each pair plays at most once).
        // Do not reuse this pattern for formats with repeat fixtures — follow the knockout-slot
        // pattern below instead (ID-first lookup, team-pair fallback).
        var fixtureByTeamPair = allDbFixtures
            .ToDictionary(f => (f.HomeTeamId, f.AwayTeamId));

        // ── 5a. Mark fixtures the API reports as postponed/cancelled ──────────
        // Postponed means delayed to an as-yet-unknown new kickoff time; Cancelled means
        // it will not be played/resumed at all. Either way, the Status is no longer
        // Scheduled, so the live-window check in step 1 stops opening for this fixture on
        // future polls — no more wasted API calls for a match that isn't on at its
        // originally scheduled time. If the fixture is later rescheduled, an admin can
        // update its KickoffUtc and Status back to Scheduled.
        var cancelledCount = 0;
        foreach (var apiFixture in allApiFixtures.Where(f => f.IsCancelledOrPostponed))
        {
            var homeCode = FootballApiTeamMap.Resolve(apiFixture.HomeTeamId, apiFixture.HomeTeamName);
            var awayCode = FootballApiTeamMap.Resolve(apiFixture.AwayTeamId, apiFixture.AwayTeamName);
            if (homeCode == null || awayCode == null) continue;
            if (!fixtureByTeamPair.TryGetValue((homeCode, awayCode), out var affectedFixture)) continue;

            var newStatus = apiFixture.IsPostponed ? MatchStatus.Postponed : MatchStatus.Cancelled;
            if (affectedFixture.Status == newStatus) continue;

            affectedFixture.Status = newStatus;
            session.Store(affectedFixture);
            cancelledCount++;
            logger.LogInformation(
                "ResultIngestionJob: fixture {Id} ({Home} vs {Away}) marked {NewStatus} — API status {Status}",
                affectedFixture.Id, homeCode, awayCode, newStatus, apiFixture.StatusShort);
        }

        // ── 6. Player lookup — served from singleton cache (roster is static) ──
        await playerCache.EnsureLoadedAsync(ct);
        var playerByName    = playerCache.ByFullName;
        var playersByLastName = playerCache.ByLastName;

        // ── 7. Process each newly-completed fixture ───────────────────────────
        var ingestedCount      = 0;
        var ingestedFixtureIds = new List<string>();

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
            // Once set, FootballApiFixtureId is never overwritten. A reschedule that bypasses
            // the postponed-recheck path could leave it pointing at a stale API ID.
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

            var varEvents  = allEvents.Where(e => e.IsVar).ToList();
            var goalEvents = FilterCancelledGoals(allEvents.Where(e => e.IsGoal && !e.IsMissedPenalty), varEvents.Where(e => e.IsGoalCancelled));
            var cardEvents = allEvents.Where(e => e.IsCard).ToList();
            var subEvents  = allEvents.Where(e => e.IsSub).ToList();

            // HARD RESET OF EVENTS AFTER ITS COMPLETION.
            // Purge events accumulated during live polling before writing the definitive FT set.
            // Runs whenever the event fetch succeeded — not guarded by !completedSet.Contains(),
            // so backfill polls (Completed + EventsIngested=false after a prior quota failure)
            // also purge before re-writing, preventing duplicates on the retry path.
            if (!eventsIngestionFailed)
            {
                await PurgeFixtureEventsAsync(session, dbFixture.Id, ct);
            }

            foreach (var evt in goalEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName)
                    continue;

                var player = ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player == null)
                {
                    logger.LogDebug(
                        "ResultIngestionJob: player '{Name}' not found in roster — goal event skipped",
                        playerName);
                    statusStore.RecordUnmatched(dbFixture.Id, "goal", playerName, evt.Time?.Elapsed ?? 0);
                    continue;
                }

                var goalType = evt.IsOwnGoal     ? GoalType.OwnGoal :
                               evt.IsPenalty     ? GoalType.PenaltyInMatch :
                                                   GoalType.OpenPlay;

                var goalId = CreateDeterministicGuid(GoalEventNamespace,
                    $"{apiFixture.FixtureId}:{player.Id}:{evt.Time?.Elapsed ?? 0}");

                session.Store(new GoalEvent
                {
                    Id          = goalId,
                    FixtureId   = dbFixture.Id,
                    PlayerId    = player.Id,
                    Type        = goalType,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
            }

            foreach (var evt in cardEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName)
                    continue;

                // Skip unknown card types
                if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed)
                    continue;

                var player = ResolvePlayer(playerName, playerByName, playersByLastName);
                if (player == null)
                {
                    logger.LogDebug(
                        "ResultIngestionJob: player '{Name}' not found in roster — card event skipped",
                        playerName);
                    statusStore.RecordUnmatched(dbFixture.Id, "card", playerName, evt.Time?.Elapsed ?? 0);
                    continue;
                }

                var cardType = evt.IsSecondYellow ? CardType.SecondYellow :
                               evt.IsRed          ? CardType.Red :
                                                    CardType.Yellow;

                var cardMinute = evt.Time?.Elapsed ?? 0;

                var cardId = CreateDeterministicGuid(GoalEventNamespace,
                    $"card:{apiFixture.FixtureId}:{player.Id}:{cardMinute}");

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

                var playerOut = ResolvePlayer(playerOutName, playerByName, playersByLastName);
                var playerIn  = ResolvePlayer(playerInName,  playerByName, playersByLastName);

                // Resolve team FIFA code from API team id
                var teamFifaCode = evt.Team?.Name is { } teamName
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, teamName) ?? string.Empty
                    : string.Empty;

                var subKey = $"sub:{apiFixture.FixtureId}:{PlayerKey(playerOutName)}:{PlayerKey(playerInName)}:{evt.Time?.Elapsed ?? 0}";
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

            foreach (var evt in varEvents)
            {
                if (evt.Player?.Name is not { Length: > 0 } playerName) continue;
                VarDecisionType? decType =
                    evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                    evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                    evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                    null;
                if (decType is null) continue;
                var varTeam = evt.Team?.Name is { } vtn
                    ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                    : string.Empty;
                var varId = CreateDeterministicGuid(GoalEventNamespace,
                    $"var:{apiFixture.FixtureId}:{decType}:{playerName}:{evt.Time?.Elapsed ?? 0}");
                session.Store(new VarEvent
                {
                    Id          = varId,
                    FixtureId   = dbFixture.Id,
                    Type        = decType.Value,
                    PlayerName  = playerName,
                    TeamId      = varTeam,
                    Minute      = evt.Time?.Elapsed ?? 0,
                    ExtraMinute = evt.Time?.Extra,
                });
            }

            if (!eventsIngestionFailed)
            {
                dbFixture.EventsIngested = true;
                session.Store(dbFixture); // LightweightSession has no dirty-tracking — mutation after Store() is not auto-picked up
            }

            ingestedFixtureIds.Add(dbFixture.Id);
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

            // Fetch live events so goal scorers and cards appear during the match.
            // Uses the same deterministic IDs as FT processing → idempotent upserts.
            IReadOnlyList<ApiMatchEvent> liveEvents = [];
            try { liveEvents = await apiClient.GetAllEventsAsync(apiFixture.FixtureId, ct); }
            catch { /* non-critical — score/clock updated above; events catch up at FT */ }

            if (liveEvents.Count > 0)
            {
                var liveVarCancels    = liveEvents.Where(e => e.IsVar && e.IsGoalCancelled);
                var liveFilteredGoals = FilterCancelledGoals(liveEvents.Where(e => e.IsGoal && !e.IsMissedPenalty), liveVarCancels);

                // Purge all event types before re-writing from the current API response.
                // Goal events alone were previously purged, but cards/subs/VAR also accumulate
                // stale rows when the PlayerKey or minute changes between live poll cycles.
                await PurgeFixtureEventsAsync(session, liveDbFixture.Id, ct);

                foreach (var evt in liveFilteredGoals)
                {
                    if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                    var liveGoalMinute = evt.Time?.Elapsed ?? 0;
                    var player = ResolvePlayer(pName, playerByName, playersByLastName);
                    if (player == null) { statusStore.RecordUnmatched(liveDbFixture.Id, "goal", pName, liveGoalMinute); continue; }
                    var gt = evt.IsOwnGoal ? GoalType.OwnGoal : evt.IsPenalty ? GoalType.PenaltyInMatch : GoalType.OpenPlay;
                    session.Store(new GoalEvent
                    {
                        Id          = CreateDeterministicGuid(GoalEventNamespace, $"{apiFixture.FixtureId}:{player.Id}:{liveGoalMinute}"),
                        FixtureId   = liveDbFixture.Id,
                        PlayerId    = player.Id,
                        Type        = gt,
                        Minute      = liveGoalMinute,
                        ExtraMinute = evt.Time?.Extra,
                    });
                }

                foreach (var evt in liveEvents.Where(e => e.IsCard))
                {
                    if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                    if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed) continue;
                    var player = ResolvePlayer(pName, playerByName, playersByLastName);
                    if (player == null) { statusStore.RecordUnmatched(liveDbFixture.Id, "card", pName, evt.Time?.Elapsed ?? 0); continue; }
                    var liveCt = evt.IsSecondYellow ? CardType.SecondYellow : evt.IsRed ? CardType.Red : CardType.Yellow;
                    var liveCardMinute = evt.Time?.Elapsed ?? 0;
                    session.Store(new CardEvent
                    {
                        Id          = CreateDeterministicGuid(GoalEventNamespace, $"card:{apiFixture.FixtureId}:{player.Id}:{liveCardMinute}"),
                        FixtureId   = liveDbFixture.Id,
                        PlayerId    = player.Id,
                        Type        = liveCt,
                        Minute      = liveCardMinute,
                        ExtraMinute = evt.Time?.Extra,
                    });
                }

                foreach (var evt in liveEvents.Where(e => e.IsSub))
                {
                    var livePlayerOutName = evt.Player?.Name ?? string.Empty;
                    var livePlayerInName  = evt.Assist?.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(livePlayerOutName) && string.IsNullOrWhiteSpace(livePlayerInName))
                        continue;
                    var liveSubTeam = evt.Team?.Name is { } stn
                        ? FootballApiTeamMap.Resolve(evt.Team.Id, stn) ?? string.Empty
                        : string.Empty;
                    var livePlayerOut = ResolvePlayer(livePlayerOutName, playerByName, playersByLastName);
                    var livePlayerIn  = ResolvePlayer(livePlayerInName,  playerByName, playersByLastName);
                    var liveSubId = CreateDeterministicGuid(GoalEventNamespace,
                        $"sub:{apiFixture.FixtureId}:{PlayerKey(livePlayerOutName)}:{PlayerKey(livePlayerInName)}:{evt.Time?.Elapsed ?? 0}");
                    session.Store(new SubstitutionEvent
                    {
                        Id            = liveSubId,
                        FixtureId     = liveDbFixture.Id,
                        PlayerOutId   = livePlayerOut?.Id,
                        PlayerInId    = livePlayerIn?.Id,
                        PlayerOutName = livePlayerOutName,
                        PlayerInName  = livePlayerInName,
                        TeamId        = liveSubTeam,
                        Minute        = evt.Time?.Elapsed ?? 0,
                        ExtraMinute   = evt.Time?.Extra,
                    });
                }

                foreach (var evt in liveEvents.Where(e => e.IsVar))
                {
                    if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                    VarDecisionType? liveDecType =
                        evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                        evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                        evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                        null;
                    if (liveDecType is null) continue;
                    var liveVarTeam = evt.Team?.Name is { } vtn
                        ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                        : string.Empty;
                    var liveVarId = CreateDeterministicGuid(GoalEventNamespace,
                        $"var:{apiFixture.FixtureId}:{liveDecType}:{pName}:{evt.Time?.Elapsed ?? 0}");
                    session.Store(new VarEvent
                    {
                        Id          = liveVarId,
                        FixtureId   = liveDbFixture.Id,
                        Type        = liveDecType.Value,
                        PlayerName  = pName,
                        TeamId      = liveVarTeam,
                        Minute      = evt.Time?.Elapsed ?? 0,
                        ExtraMinute = evt.Time?.Extra,
                    });
                }
            }
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
                     && s.Status != MatchStatus.Cancelled
                     && s.Status != MatchStatus.Postponed)
            .ToListAsync(ct);

        var knockoutUpdated      = 0;
        var completedSlotKeys    = new List<string>();
        if (knockoutSlots.Count > 0)
        {
            var knockoutByApiId = knockoutSlots
                .Where(s => s.FootballApiFixtureId.HasValue)
                .ToDictionary(s => s.FootballApiFixtureId!.Value);

            var knockoutByTeamPair = knockoutSlots
                .ToDictionary(s => (s.HomeTeamId!, s.AwayTeamId!));

            var touchedSlotKeys = new HashSet<string>();

            foreach (var apiFixture in allApiFixtures.Where(f => f.IsLive || f.IsFullTime || f.IsCancelledOrPostponed))
            {
                // Prefer ID-based match (fast, unambiguous); fall back to team-pair on first contact.
                KnockoutSlot? slot;
                if (!knockoutByApiId.TryGetValue(apiFixture.FixtureId, out slot))
                {
                    var homeCode = FootballApiTeamMap.Resolve(apiFixture.HomeTeamId, apiFixture.HomeTeamName);
                    var awayCode = FootballApiTeamMap.Resolve(apiFixture.AwayTeamId, apiFixture.AwayTeamName);
                    if (homeCode == null || awayCode == null) continue;
                    if (!knockoutByTeamPair.TryGetValue((homeCode, awayCode), out slot)) continue;

                    // Store the API fixture ID so future polls use the faster ID-based path.
                    slot.FootballApiFixtureId = apiFixture.FixtureId;
                }
                // slot is guaranteed non-null here (both branches above either continue or set it).

                if (!touchedSlotKeys.Add(slot.SlotKey))
                {
                    logger.LogWarning(
                        "ResultIngestionJob: knockout slot {SlotKey} resolved twice in one poll — possible team-pair resolution collision, skipping duplicate",
                        slot.SlotKey);
                    continue;
                }

                if (apiFixture.IsCancelledOrPostponed)
                {
                    var newSlotStatus = apiFixture.IsPostponed ? MatchStatus.Postponed : MatchStatus.Cancelled;
                    slot.Status = newSlotStatus;
                    session.Store(slot);
                    cancelledCount++;
                    logger.LogInformation(
                        "ResultIngestionJob: knockout slot {SlotKey} ({Home} vs {Away}) marked {NewStatus} — API status {Status}",
                        slot.SlotKey, slot.HomeTeamId, slot.AwayTeamId, newSlotStatus, apiFixture.StatusShort);
                    continue;
                }

                if (apiFixture.IsLive)
                {
                    slot.Status = apiFixture.StatusShort is "ET" or "BT"
                                ? MatchStatus.ExtraTime
                                : apiFixture.StatusShort is "P"
                                ? MatchStatus.PenaltyShootout
                                : MatchStatus.InProgress;
                    slot.HomeScore = apiFixture.HomeGoals;
                    slot.AwayScore = apiFixture.AwayGoals;
                    // WinnerTeamId is only valid when Status == Completed; clear on regression.
                    slot.WinnerTeamId     = null;
                    slot.PenaltyHomeScore = null;
                    slot.PenaltyAwayScore = null;
                    if (slot.Status == MatchStatus.PenaltyShootout)
                    {
                        slot.PenaltyHomeScore = apiFixture.ScorePenaltyHome;
                        slot.PenaltyAwayScore = apiFixture.ScorePenaltyAway;
                    }

                    // Live events for knockout slots — mirrors step 8a's group-fixture logic.
                    // GoalEvent.FixtureId = slot.SlotKey (e.g. "R32-1"): safe because the
                    // scoring service counts goals globally by PlayerId and never joins through
                    // FixtureId; SlotKey values are non-colliding with group Fixture.Id strings.
                    IReadOnlyList<ApiMatchEvent> liveSlotEvents = [];
                    try { liveSlotEvents = await apiClient.GetAllEventsAsync(apiFixture.FixtureId, ct); }
                    catch { /* non-critical — score/status already updated above; events catch up at FT */ }

                    if (liveSlotEvents.Count > 0)
                    {
                        // During a penalty shootout, API-Football emits each kick as
                        // type:"Goal", detail:"Penalty" — the same shape as an in-match penalty.
                        // The only distinguishing field is comments:"Penalty Shootout", which is
                        // not mapped. Skip goal purge+storage during shootout so regulation/ET
                        // goals settled on prior polls are preserved — they are never re-written
                        // by the FT branch and would be permanently lost if purged here.
                        // Cards/subs/VAR use deterministic IDs and are safe upserts; they run
                        // regardless of shootout status to capture any events during the shootout.
                        if (ShouldPurgeGoalEventsOnLivePoll(slot.Status))
                        {
                            await PurgeFixtureEventsAsync(session, slot.SlotKey, ct);

                            var slotVarCancels    = liveSlotEvents.Where(e => e.IsVar && e.IsGoalCancelled);
                            var slotFilteredGoals = FilterCancelledGoals(liveSlotEvents.Where(e => e.IsGoal && !e.IsMissedPenalty), slotVarCancels);

                            foreach (var evt in slotFilteredGoals)
                            {
                                if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                                var slotGoalMinute = evt.Time?.Elapsed ?? 0;
                                var slotPlayer = ResolvePlayer(pName, playerByName, playersByLastName);
                                if (slotPlayer == null) { statusStore.RecordUnmatched(slot.SlotKey, "goal", pName, slotGoalMinute); continue; }
                                var slotGt = evt.IsOwnGoal ? GoalType.OwnGoal : evt.IsPenalty ? GoalType.PenaltyInMatch : GoalType.OpenPlay;
                                session.Store(new GoalEvent
                                {
                                    Id          = CreateDeterministicGuid(GoalEventNamespace, $"{apiFixture.FixtureId}:{slotPlayer.Id}:{slotGoalMinute}"),
                                    FixtureId   = slot.SlotKey,
                                    PlayerId    = slotPlayer.Id,
                                    Type        = slotGt,
                                    Minute      = slotGoalMinute,
                                    ExtraMinute = evt.Time?.Extra,
                                });
                            }
                        }

                        foreach (var evt in liveSlotEvents.Where(e => e.IsCard))
                        {
                            if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                            if (!evt.IsYellow && !evt.IsSecondYellow && !evt.IsRed) continue;
                            var slotPlayer = ResolvePlayer(pName, playerByName, playersByLastName);
                            if (slotPlayer == null) { statusStore.RecordUnmatched(slot.SlotKey, "card", pName, evt.Time?.Elapsed ?? 0); continue; }
                            var slotCt = evt.IsSecondYellow ? CardType.SecondYellow : evt.IsRed ? CardType.Red : CardType.Yellow;
                            var slotCardMinute = evt.Time?.Elapsed ?? 0;
                            session.Store(new CardEvent
                            {
                                Id          = CreateDeterministicGuid(GoalEventNamespace, $"card:{apiFixture.FixtureId}:{slotPlayer.Id}:{slotCardMinute}"),
                                FixtureId   = slot.SlotKey,
                                PlayerId    = slotPlayer.Id,
                                Type        = slotCt,
                                Minute      = slotCardMinute,
                                ExtraMinute = evt.Time?.Extra,
                            });
                        }

                        foreach (var evt in liveSlotEvents.Where(e => e.IsSub))
                        {
                            var slotPlayerOutName = evt.Player?.Name ?? string.Empty;
                            var slotPlayerInName  = evt.Assist?.Name ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(slotPlayerOutName) && string.IsNullOrWhiteSpace(slotPlayerInName)) continue;
                            var slotSubTeam = evt.Team?.Name is { } stn
                                ? FootballApiTeamMap.Resolve(evt.Team.Id, stn) ?? string.Empty
                                : string.Empty;
                            var slotPlayerOut = ResolvePlayer(slotPlayerOutName, playerByName, playersByLastName);
                            var slotPlayerIn  = ResolvePlayer(slotPlayerInName,  playerByName, playersByLastName);
                            var slotSubId = CreateDeterministicGuid(GoalEventNamespace,
                                $"sub:{apiFixture.FixtureId}:{PlayerKey(slotPlayerOutName)}:{PlayerKey(slotPlayerInName)}:{evt.Time?.Elapsed ?? 0}");
                            session.Store(new SubstitutionEvent
                            {
                                Id            = slotSubId,
                                FixtureId     = slot.SlotKey,
                                PlayerOutId   = slotPlayerOut?.Id,
                                PlayerInId    = slotPlayerIn?.Id,
                                PlayerOutName = slotPlayerOutName,
                                PlayerInName  = slotPlayerInName,
                                TeamId        = slotSubTeam,
                                Minute        = evt.Time?.Elapsed ?? 0,
                                ExtraMinute   = evt.Time?.Extra,
                            });
                        }

                        foreach (var evt in liveSlotEvents.Where(e => e.IsVar))
                        {
                            if (evt.Player?.Name is not { Length: > 0 } pName) continue;
                            VarDecisionType? slotDecType =
                                evt.IsGoalCancelled     ? VarDecisionType.GoalCancelled :
                                evt.IsCardUpgradeRed    ? VarDecisionType.CardUpgradeRed :
                                evt.IsCardUpgrade2ndYel ? VarDecisionType.CardUpgradeSecondYellow :
                                null;
                            if (slotDecType is null) continue;
                            var slotVarTeam = evt.Team?.Name is { } vtn
                                ? FootballApiTeamMap.Resolve(evt.Team.Id, vtn) ?? string.Empty
                                : string.Empty;
                            var slotVarId = CreateDeterministicGuid(GoalEventNamespace,
                                $"var:{apiFixture.FixtureId}:{slotDecType}:{pName}:{evt.Time?.Elapsed ?? 0}");
                            session.Store(new VarEvent
                            {
                                Id          = slotVarId,
                                FixtureId   = slot.SlotKey,
                                Type        = slotDecType.Value,
                                PlayerName  = pName,
                                TeamId      = slotVarTeam,
                                Minute      = evt.Time?.Elapsed ?? 0,
                                ExtraMinute = evt.Time?.Extra,
                            });
                        }
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

                    completedSlotKeys.Add(slot.SlotKey);
                    logger.LogInformation(
                        "ResultIngestionJob: knockout slot {SlotKey} completed — {Home} {HomeScore}-{AwayScore} {Away}, winner={Winner}",
                        slot.SlotKey, slot.HomeTeamId, slot.HomeScore, slot.AwayScore, slot.AwayTeamId,
                        slot.WinnerTeamId ?? "TBD");
                }

                session.Store(slot);
                knockoutUpdated++;
            }
        }

        if (ingestedCount > 0 || anyLive || knockoutUpdated > 0 || cancelledCount > 0)
        {
            await session.SaveChangesAsync(ct);
            await outputCache.EvictByTagAsync("fixtures", ct);
            await outputCache.EvictByTagAsync("knockout-slots", ct);
        }

        if (ingestedCount > 0 || knockoutUpdated > 0)
        {
            logger.LogInformation(
                "ResultIngestionJob: {GroupCount} group fixture(s), {KnockoutCount} knockout slot(s) updated — triggering bracket resolution and score recompute",
                ingestedCount, knockoutUpdated);

            // Reuse the same session so bracket + scoring writes flush in a single
            // SaveChangesAsync below instead of two extra round-trips. Ingestion data
            // was already committed above, so the resolver's reads see the latest state.
            await bracketResolver.ResolveGroupStageAsync(session, ct);
            await bracketResolver.PropagateAllKnockoutResultsAsync(session, ct);

            var timeSinceLastRecompute = now - statusStore.LastRecomputeUtc;
            if (timeSinceLastRecompute < RecomputeMinInterval)
            {
                logger.LogDebug(
                    "ResultIngestionJob: skipping RecomputeAllAsync — last run was {Elapsed:0.0}s ago (min interval {Min}s)",
                    timeSinceLastRecompute?.TotalSeconds ?? 0, RecomputeMinInterval.TotalSeconds);
            }
            else
            {
                statusStore.LastRecomputeUtc = DateTimeOffset.UtcNow;
                await scoringService.RecomputeForCompletedAsync(ingestedFixtureIds, completedSlotKeys, session, ct);
            }

            await session.SaveChangesAsync(ct);
            await outputCache.EvictByTagAsync("leaderboard", ct);
        }

        // ── Record successful poll ────────────────────────────────────────────
        statusStore.LastSuccessfulPoll = DateTimeOffset.UtcNow;
        statusStore.LastError = null;

        logger.LogDebug("ResultIngestionJob: completed (ingested={Count}, liveWindow={Live})",
            ingestedCount, anyLive);
    }

    /// <summary>
    /// Rechecks every Postponed Fixture/KnockoutSlot against the full-season API fixture
    /// list (since a postponed fixture's original date is no longer "today", the per-date
    /// fetch in the main pipeline won't surface it). Costs one API request, throttled by
    /// the caller to roughly once a minute.
    ///
    /// Outcomes:
    ///   - API reports it cancelled/abandoned/awarded/walkover/suspended → Status=Cancelled.
    ///     No further processing — there's nothing to ingest for a match that won't happen.
    ///   - API still reports it postponed → stays Postponed; KickoffUtc is updated if the
    ///     API has since announced a tentative new date.
    ///   - API reports anything else (a concrete new kickoff, already live, or already
    ///     full-time) → Status=Scheduled with KickoffUtc set from the API's date. This puts
    ///     the fixture back into the normal pipeline, which will pick it up on this or a
    ///     subsequent cycle and ingest it exactly like any other match — "consume as normal".
    /// </summary>
    private async Task RecheckPostponedFixturesAsync(CancellationToken ct)
    {
        await using var session = store.LightweightSession();

        var postponedFixtures = await session
            .Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Postponed)
            .ToListAsync(ct);

        var postponedSlots = await session
            .Query<KnockoutSlot>()
            .Where(s => s.Status == MatchStatus.Postponed)
            .ToListAsync(ct);

        if (postponedFixtures.Count == 0 && postponedSlots.Count == 0)
            return;

        IReadOnlyList<ApiFixture> allApiFixtures;
        try
        {
            allApiFixtures = await apiClient.GetAllFixturesForSeasonAsync(ct);
        }
        catch (HttpRequestException ex) when (ex.InnerException is InvalidOperationException { Message: "Quota exceeded" })
        {
            statusStore.LastError = "API quota exhausted (429) — postponed recheck deferred";
            statusStore.ErrorCount++;
            logger.LogWarning(ex, "ResultIngestionJob: postponed recheck deferred — API quota exhausted");
            return;
        }
        catch (Exception ex)
        {
            statusStore.LastError = ex.Message;
            statusStore.ErrorCount++;
            logger.LogWarning(ex,
                "ResultIngestionJob: failed to recheck postponed fixtures — will retry on next scheduled recheck");
            return;
        }

        var apiFixtureByTeamPair = new Dictionary<(string Home, string Away), ApiFixture>();
        foreach (var af in allApiFixtures)
        {
            var homeCode = FootballApiTeamMap.Resolve(af.HomeTeamId, af.HomeTeamName);
            var awayCode = FootballApiTeamMap.Resolve(af.AwayTeamId, af.AwayTeamName);
            if (homeCode == null || awayCode == null) continue;
            apiFixtureByTeamPair[(homeCode, awayCode)] = af;
        }

        var updated = 0;

        foreach (var fixture in postponedFixtures)
        {
            if (!apiFixtureByTeamPair.TryGetValue((fixture.HomeTeamId, fixture.AwayTeamId), out var apiFixture))
                continue;

            if (ApplyPostponedRecheck(fixture.Id, apiFixture,
                    s => fixture.Status = s, d => fixture.KickoffUtc = d))
            {
                session.Store(fixture);
                updated++;
            }
        }

        foreach (var slot in postponedSlots)
        {
            if (slot.HomeTeamId is null || slot.AwayTeamId is null) continue;
            if (!apiFixtureByTeamPair.TryGetValue((slot.HomeTeamId, slot.AwayTeamId), out var apiFixture))
                continue;

            if (ApplyPostponedRecheck(slot.SlotKey, apiFixture,
                    s => slot.Status = s, d => slot.KickoffUtc = d))
            {
                session.Store(slot);
                updated++;
            }
        }

        if (updated > 0)
        {
            await session.SaveChangesAsync(ct);
            await outputCache.EvictByTagAsync("fixtures", ct);
            await outputCache.EvictByTagAsync("knockout-slots", ct);
        }
    }

    /// <summary>
    /// Pure decision logic for a single postponed fixture/slot recheck. Returns true if the
    /// caller's setters were invoked (i.e. something changed and needs to be persisted).
    /// </summary>
    private bool ApplyPostponedRecheck(
        string id,
        ApiFixture apiFixture,
        Action<MatchStatus> setStatus,
        Action<DateTimeOffset> setKickoffUtc)
    {
        if (apiFixture.IsCancelled)
        {
            setStatus(MatchStatus.Cancelled);
            logger.LogInformation(
                "ResultIngestionJob: postponed fixture/slot {Id} marked Cancelled — API status {Status}",
                id, apiFixture.StatusShort);
            return true;
        }

        if (apiFixture.IsPostponed)
        {
            // Still postponed — only persist a change if the API has since announced a
            // tentative new date for it.
            if (DateTimeOffset.TryParse(apiFixture.Date, out var tentativeDate))
            {
                setKickoffUtc(tentativeDate);
                logger.LogDebug(
                    "ResultIngestionJob: postponed fixture/slot {Id} still postponed — tentative new kickoff {Kickoff}",
                    id, tentativeDate);
                return true;
            }
            logger.LogDebug(
                "ResultIngestionJob: postponed fixture/slot {Id} still postponed — could not parse API date '{Date}', no change applied",
                id, apiFixture.Date);
            return false;
        }

        // Anything else (a concrete new kickoff, already live, or already full-time) means
        // the fixture is back on track — hand it back to the normal Scheduled pipeline.
        setStatus(MatchStatus.Scheduled);
        if (DateTimeOffset.TryParse(apiFixture.Date, out var newKickoff))
            setKickoffUtc(newKickoff);

        logger.LogInformation(
            "ResultIngestionJob: postponed fixture/slot {Id} resumed (API status {Status}) — " +
            "marked Scheduled and will be ingested normally",
            id, apiFixture.StatusShort);
        return true;
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
    /// Resolves a player from the API event name using two steps:
    /// 1. Exact full-name match (case-insensitive) — handles "Themba Zwane", "César Montes".
    /// 2. Last-name match — extracts the last word of the API name and compares it against
    ///    the last word of every DB player name. Handles abbreviated API names like "F. Balogun".
    ///    When multiple DB players share a last name, the first-letter initial from an
    ///    abbreviated API name (e.g. "T." in "T. Adams") is used to disambiguate.
    /// Returns null when the name cannot be resolved unambiguously.
    /// </summary>
    internal static Player? ResolvePlayer(
        string apiName,
        IReadOnlyDictionary<string, Player> byFullName,
        ILookup<string, Player> byLastName)
    {
        if (string.IsNullOrWhiteSpace(apiName)) return null;

        // 1. Exact full-name match — normalized so "Brian Gutierrez" matches "Brian Gutiérrez",
        //    "Ostigard" matches "Østigård", "Al Arab" matches "Al-Arab", etc.
        if (byFullName.TryGetValue(NormalizeName(apiName), out var exact)) return exact;

        // 2. Last-name match — compare last word of API name against last word of DB name
        var lastWord = LastWord(apiName);
        var candidates = byLastName[lastWord].ToList();

        if (candidates.Count == 1) return candidates[0];
        if (candidates.Count == 0) return null;

        // Multiple DB players share this last name — try first-initial disambiguation.
        // API abbreviated format: "T. Adams" → first part is "T.", initial is 'T'.
        var parts = apiName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && parts[0].Length == 2 && parts[0][1] == '.')
        {
            var initial = char.ToUpperInvariant(parts[0][0]);
            var byInitial = candidates
                .Where(p => char.ToUpperInvariant(p.Name[0]) == initial)
                .ToList();
            if (byInitial.Count == 1) return byInitial[0];
        }

        return null;
    }

    internal static string LastWord(string name)
    {
        var normalized = NormalizeName(name);
        var idx = normalized.LastIndexOf(' ');
        return idx < 0 ? normalized : normalized[(idx + 1)..];
    }

    /// <summary>
    /// Strips diacritical marks (accents, tildes, umlauts) from a string so that
    /// "Quiñones" == "Quinones", "Jiménez" == "Jimenez", "Gutiérrez" == "Gutierrez".
    /// Uses Unicode FormD decomposition to separate base letters from combining marks,
    /// removes the combining marks, then recomposes to FormC.
    /// </summary>
    internal static string StripDiacritics(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>
    /// Full normalisation for player-name matching. Handles characters that Unicode FormD
    /// cannot decompose (Ø→O, ø→o, ß→ss, ı→i for Turkish dotless-i), collapses hyphens
    /// to spaces (Al-Arab → Al Arab, Heung-min → Heung min) and removes apostrophes
    /// (O'Neill → ONeill), then strips all remaining diacritical marks via
    /// <see cref="StripDiacritics"/>. Both the DB-side lookup keys and the API-side query
    /// strings must be processed through this method so comparisons are symmetric.
    /// </summary>
    internal static string NormalizeName(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            switch (c)
            {
                case 'Ø': sb.Append('O'); break;
                case 'ø': sb.Append('o'); break;
                case 'Æ': sb.Append("AE"); break;
                case 'æ': sb.Append("ae"); break;
                case 'ı': sb.Append('i'); break; // Turkish dotless i
                case 'ß': sb.Append("ss"); break;
                case '-': sb.Append(' '); break; // Al-Arab → Al Arab
                case '\'': break;                // O'Neill → ONeill
                default: sb.Append(c); break;
            }
        }
        return StripDiacritics(sb.ToString());
    }

    /// <summary>
    /// Stable key for a substitution event-ID hash. Always uses the normalized raw name —
    /// never the resolved player ID — so the key is stable across polls regardless of
    /// whether player resolution succeeds. Resolution outcome varies with cache state and
    /// can cause duplicate SubstitutionEvent documents if the key changes between polls.
    /// Combined with PurgeFixtureEventsAsync before each re-write, this guarantees no
    /// stale rows survive even when the raw name format shifts between calls.
    /// </summary>
    internal static string PlayerKey(string rawName) =>
        NormalizeName(rawName).Trim().ToLowerInvariant();

    /// <summary>
    /// Returns true when goal events should be purged and rewritten on a live-poll cycle.
    /// False during <see cref="MatchStatus.PenaltyShootout"/>: API-Football emits each
    /// shootout kick as a "Penalty" goal event, indistinguishable from in-match penalties,
    /// so purging here would delete the regulation/ET goals with no path to restore them.
    /// Pure function — testable without any infrastructure.
    /// </summary>
    internal static bool ShouldPurgeGoalEventsOnLivePoll(MatchStatus slotStatus)
        => slotStatus != MatchStatus.PenaltyShootout;

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
        var minute = evt.Time?.Elapsed ?? 0;

        return new GoalEvent
        {
            Id          = CreateDeterministicGuid(GoalEventNamespace,
                              $"{apiFixtureId}:{playerId}:{minute}"),
            FixtureId   = dbFixtureId,
            PlayerId    = playerId,
            Type        = MapGoalType(evt),
            Minute      = minute,
            ExtraMinute = evt.Time?.Extra,
        };
    }

    /// <summary>
    /// Deletes all goal, card, substitution, and VAR event documents for the given fixture/slot ID.
    /// Used before re-writing a complete event set (live poll cycle, FT transition, or backfill retry)
    /// so that stale rows from a prior poll never survive as duplicates.
    /// </summary>
    private static async Task PurgeFixtureEventsAsync(IDocumentSession session, string fixtureId, CancellationToken ct)
    {
        var staleGoals = await session.Query<GoalEvent>().Where(g => g.FixtureId == fixtureId).ToListAsync(ct);
        var staleCards = await session.Query<CardEvent>().Where(c => c.FixtureId == fixtureId).ToListAsync(ct);
        var staleSubs  = await session.Query<SubstitutionEvent>().Where(s => s.FixtureId == fixtureId).ToListAsync(ct);
        var staleVars  = await session.Query<VarEvent>().Where(v => v.FixtureId == fixtureId).ToListAsync(ct);
        foreach (var g in staleGoals) session.Delete(g);
        foreach (var c in staleCards) session.Delete(c);
        foreach (var s in staleSubs)  session.Delete(s);
        foreach (var v in staleVars)  session.Delete(v);
    }

    /// <summary>
    /// Removes goal events that were subsequently cancelled by a VAR decision.
    /// For each VAR GoalCancelled event, finds the most recent goal by the same player
    /// at or before the VAR minute and drops it from the list.
    /// </summary>
    internal static List<ApiMatchEvent> FilterCancelledGoals(
        IEnumerable<ApiMatchEvent> goalEvents,
        IEnumerable<ApiMatchEvent> varCancellations)
    {
        var cancels = varCancellations.ToList();
        if (cancels.Count == 0) return goalEvents.ToList();

        var remaining = goalEvents.ToList();
        foreach (var varEvt in cancels)
        {
            var name = varEvt.Player?.Name;
            if (string.IsNullOrEmpty(name)) continue;
            var varMinute = varEvt.Time?.Elapsed ?? int.MaxValue;
            var normalizedVarName = NormalizeName(name);

            // Normalize both sides so "M. De Cuyper" (goal event) matches "Maxim De Cuyper"
            // (VAR event) — API-Football is inconsistent about name formats across event types.
            var target = remaining
                .Where(g => g.Player?.Name is { } gName
                         && string.Equals(NormalizeName(gName), normalizedVarName, StringComparison.OrdinalIgnoreCase)
                         && (g.Time?.Elapsed ?? 0) <= varMinute)
                .OrderByDescending(g => g.Time?.Elapsed ?? 0)
                .FirstOrDefault();

            if (target != null) remaining.Remove(target);
        }
        return remaining;
    }
}
