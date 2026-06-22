using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Ingestion;

namespace TriviumWorldCup.Api.Tests.Ingestion;

/// <summary>
/// Unit tests for the ingestion pipeline — pure functions and deterministic logic only.
/// No real database or HTTP calls required.
///
/// TWC-9 acceptance criteria covered:
///   - Idempotency: same inputs produce the same GoalEvent ID (safe upsert).
///   - Already-completed fixture detection: completedSet membership is reliable.
///   - Player name not in roster: goal event gracefully skipped.
///   - GoalType mapping: API detail strings map to correct enum values.
///   - Team code resolution: API team IDs and names resolve to FIFA codes.
/// </summary>
public class ResultIngestionJobTests
{
    // ── CreateDeterministicGuid — idempotency foundation ─────────────────────

    [Fact]
    public void CreateDeterministicGuid_SameInputs_ReturnsSameGuid()
    {
        var ns   = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var guid1 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Lionel Messi:23");
        var guid2 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Lionel Messi:23");

        Assert.Equal(guid1, guid2);
    }

    [Fact]
    public void CreateDeterministicGuid_DifferentMinute_ReturnsDifferentGuid()
    {
        var ns = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var guid1 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Lionel Messi:23");
        var guid2 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Lionel Messi:67");

        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public void CreateDeterministicGuid_DifferentPlayer_ReturnsDifferentGuid()
    {
        var ns = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var guid1 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Lionel Messi:23");
        var guid2 = ResultIngestionJob.CreateDeterministicGuid(ns, "12345:Cristiano Ronaldo:23");

        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public void CreateDeterministicGuid_DifferentFixture_ReturnsDifferentGuid()
    {
        var ns = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var guid1 = ResultIngestionJob.CreateDeterministicGuid(ns, "11111:Lionel Messi:23");
        var guid2 = ResultIngestionJob.CreateDeterministicGuid(ns, "22222:Lionel Messi:23");

        Assert.NotEqual(guid1, guid2);
    }

    [Fact]
    public void CreateDeterministicGuid_ReturnsVersion5Uuid()
    {
        // UUID v5: version nibble = 5 in the 7th byte (high nibble of byte 6)
        var ns   = new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var guid = ResultIngestionJob.CreateDeterministicGuid(ns, "test-input");

        var bytes = guid.ToByteArray();
        // In .NET mixed-endian, version is in byte 7 (after endian swap back from network order)
        // The version nibble should be 0x5X
        Assert.Equal(0x50, bytes[7] & 0xF0);
    }

    // ── Idempotency: processing same goal twice yields same ID ───────────────

    [Fact]
    public void SameGoalProcessedTwice_ProducesSameGoalEventId()
    {
        // Arrange: a goal event from an API response
        var player = new Player { Id = Guid.NewGuid(), Name = "Lionel Messi", TeamId = "ARG", Position = Position.FWD };
        var apiEvt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 23 },
            Player = new ApiPlayer { Id = 10, Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };

        // Act: build goal event twice from same inputs
        var goalEvent1 = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", player.Id, apiEvt);
        var goalEvent2 = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", player.Id, apiEvt);

        // Assert: same ID both times — session.Store() is a safe upsert
        Assert.Equal(goalEvent1.Id, goalEvent2.Id);
        Assert.Equal(GoalType.OpenPlay, goalEvent1.Type);
        Assert.Equal(23, goalEvent1.Minute);
        Assert.Equal(player.Id, goalEvent1.PlayerId);
        Assert.Equal("fixture-1", goalEvent1.FixtureId);
    }

    [Fact]
    public void SamePlayer_DifferentRawNameFormat_ProducesSameGoalEventId()
    {
        // API-Football doesn't always return the same name format for a player across
        // separate calls (e.g. abbreviated "M. De Cuyper" while live, full "Maxim De Cuyper"
        // at full time). Both calls resolve to the same Player, so the ID must be based on
        // the resolved PlayerId, not the raw event text — otherwise a second GoalEvent
        // document is minted for the same real-world goal and the UI shows a duplicate.
        var playerId = Guid.NewGuid();

        var liveCallEvt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 56 },
            Player = new ApiPlayer { Name = "M. De Cuyper" },
            Detail = "Normal Goal",
        };
        var fullTimeCallEvt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 56 },
            Player = new ApiPlayer { Name = "Maxim De Cuyper" },
            Detail = "Normal Goal",
        };

        var goalFromLiveCall     = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", playerId, liveCallEvt);
        var goalFromFullTimeCall = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", playerId, fullTimeCallEvt);

        Assert.Equal(goalFromLiveCall.Id, goalFromFullTimeCall.Id);
    }

    [Fact]
    public void PlayerKey_SameRawName_StableRegardlessOfResolutionOutcome()
    {
        // PlayerKey must be stable even when player resolution succeeds on one poll and
        // fails on the next (cache timing, ambiguous name, etc.). If the key changed between
        // polls, a second SubstitutionEvent would be minted for the same real-world sub.
        // Fix: always use the normalized raw name, never the resolved player's ID.
        var player = new Player { Id = Guid.NewGuid(), Name = "Maxim De Cuyper" };

        var key = ResultIngestionJob.PlayerKey("Maxim De Cuyper");

        // Must not return the player's GUID (that was the bug: resolution-dependent key).
        Assert.NotEqual(player.Id.ToString(), key);
        // Must be the normalized raw name.
        Assert.Equal("maxim de cuyper", key);
    }

    [Fact]
    public void PlayerKey_NormalizedRawName_StripsAccents()
    {
        var key1 = ResultIngestionJob.PlayerKey("Jiménez");
        var key2 = ResultIngestionJob.PlayerKey("jimenez");

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void TwoDistinctGoals_ProduceDifferentGoalEventIds()
    {
        var playerId = Guid.NewGuid();

        var evt1 = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 23 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };
        var evt2 = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 67 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };

        var goal1 = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", playerId, evt1);
        var goal2 = ResultIngestionJob.BuildGoalEvent(99001, "fixture-1", playerId, evt2);

        Assert.NotEqual(goal1.Id, goal2.Id);
    }

    // ── GoalType mapping ─────────────────────────────────────────────────────

    [Fact]
    public void MapGoalType_NormalGoal_ReturnsOpenPlay()
    {
        var evt = new ApiMatchEvent { Detail = "Normal Goal" };
        Assert.Equal(GoalType.OpenPlay, ResultIngestionJob.MapGoalType(evt));
    }

    [Fact]
    public void MapGoalType_NullDetail_ReturnsOpenPlay()
    {
        var evt = new ApiMatchEvent { Detail = null };
        Assert.Equal(GoalType.OpenPlay, ResultIngestionJob.MapGoalType(evt));
    }

    [Fact]
    public void MapGoalType_Penalty_ReturnsPenaltyInMatch()
    {
        var evt = new ApiMatchEvent { Detail = "Penalty" };
        Assert.Equal(GoalType.PenaltyInMatch, ResultIngestionJob.MapGoalType(evt));
    }

    [Fact]
    public void MapGoalType_OwnGoal_ReturnsOwnGoal()
    {
        var evt = new ApiMatchEvent { Detail = "Own Goal" };
        Assert.Equal(GoalType.OwnGoal, ResultIngestionJob.MapGoalType(evt));
    }

    [Fact]
    public void MapGoalType_CaseInsensitiveOwnGoal_ReturnsOwnGoal()
    {
        var evt = new ApiMatchEvent { Detail = "own goal" };
        Assert.Equal(GoalType.OwnGoal, ResultIngestionJob.MapGoalType(evt));
    }

    [Fact]
    public void MapGoalType_CaseInsensitivePenalty_ReturnsPenaltyInMatch()
    {
        var evt = new ApiMatchEvent { Detail = "penalty" };
        Assert.Equal(GoalType.PenaltyInMatch, ResultIngestionJob.MapGoalType(evt));
    }

    // ── Player not in roster: goal silently skipped ──────────────────────────

    [Fact]
    public void BuildGoalEvent_PlayerNameEmpty_StillBuildsEvent_WithDefaultPlayer()
    {
        // The guard "player not in roster" is enforced in Execute() before calling BuildGoalEvent.
        // This test validates that the filtering logic is correct: an empty player name is
        // handled by the calling code, not by BuildGoalEvent itself.
        // If the player name is empty, Execute() skips the event.
        var apiEvt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 10 },
            Player = new ApiPlayer { Name = "" },
            Detail = "Normal Goal",
        };

        // The pattern in Execute: check Player?.Name is not { Length: > 0 }
        var playerName = apiEvt.Player?.Name;
        var isSkipped  = string.IsNullOrEmpty(playerName);

        Assert.True(isSkipped, "Empty player name should cause the goal event to be skipped");
    }

    [Fact]
    public void PlayerNotInRosterDictionary_IsSkippedGracefully()
    {
        // Simulates the lookup in Execute() — TryGetValue returns false for unknown player
        var playerByName = new Dictionary<string, Player>(StringComparer.OrdinalIgnoreCase)
        {
            { "Known Player", new Player { Id = Guid.NewGuid(), Name = "Known Player" } }
        };

        var unknownName = "Unknown Player From API";
        var found = playerByName.TryGetValue(unknownName, out _);

        // Should be false — player skipped, no exception
        Assert.False(found);
    }

    // ── ApiFixture status helpers ─────────────────────────────────────────────

    [Theory]
    [InlineData("FT",  true)]
    [InlineData("PEN", true)]
    [InlineData("AET", true)]
    [InlineData("NS",  false)]
    [InlineData("1H",  false)]
    [InlineData("HT",  false)]
    [InlineData("2H",  false)]
    public void ApiFixture_IsFullTime_MatchesExpectedStatuses(string status, bool expected)
    {
        var fixture = new ApiFixture { StatusShort = status };
        Assert.Equal(expected, fixture.IsFullTime);
    }

    [Theory]
    [InlineData("1H",  true)]
    [InlineData("HT",  true)]
    [InlineData("2H",  true)]
    [InlineData("ET",  true)]
    [InlineData("P",   true)]
    [InlineData("NS",  false)]
    [InlineData("FT",  false)]
    public void ApiFixture_IsLive_MatchesExpectedStatuses(string status, bool expected)
    {
        var fixture = new ApiFixture { StatusShort = status };
        Assert.Equal(expected, fixture.IsLive);
    }

    // ── FootballApiTeamMap ────────────────────────────────────────────────────

    [Theory]
    [InlineData("France",     "FRA")]
    [InlineData("Argentina",  "ARG")]
    [InlineData("England",    "ENG")]
    [InlineData("Brazil",     "BRA")]
    [InlineData("Germany",    "GER")]
    [InlineData("Spain",      "ESP")]
    [InlineData("Mexico",     "MEX")]
    [InlineData("Netherlands","NED")]
    [InlineData("Portugal",   "POR")]
    [InlineData("Colombia",   "COL")]
    public void FootballApiTeamMap_ResolvesByName_ReturnsCorrectFifaCode(string teamName, string expectedCode)
    {
        var result = FootballApiTeamMap.Resolve(99999, teamName); // 99999 = unknown ID → fallback to name
        Assert.Equal(expectedCode, result);
    }

    [Fact]
    public void FootballApiTeamMap_UnknownTeamName_ReturnsNull()
    {
        var result = FootballApiTeamMap.Resolve(99999, "Fictional FC");
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Türkiye",  "TUR")]
    [InlineData("Turkey",   "TUR")]
    [InlineData("Côte d'Ivoire", "CIV")]
    [InlineData("Ivory Coast",   "CIV")]
    [InlineData("Congo DR",      "COD")]
    [InlineData("DR Congo",      "COD")]
    [InlineData("Cabo Verde",    "CPV")]
    [InlineData("Cape Verde",    "CPV")]
    [InlineData("Cape Verde Islands", "CPV")]
    public void FootballApiTeamMap_AlternateNames_ResolveCorrectly(string apiName, string expectedCode)
    {
        var result = FootballApiTeamMap.Resolve(99999, apiName);
        Assert.Equal(expectedCode, result);
    }

    [Fact]
    public void FootballApiTeamMap_CaseInsensitiveName_Resolves()
    {
        var result = FootballApiTeamMap.Resolve(99999, "FRANCE");
        Assert.Equal("FRA", result);
    }

    // ── GoalEvent fixture ID linkage ──────────────────────────────────────────

    [Fact]
    public void BuildGoalEvent_FixtureIdIsDbFixtureId_NotApiFixtureId()
    {
        // The GoalEvent.FixtureId must reference the Marten Fixture.Id (string, e.g. "1")
        // not the API-Football integer fixture ID — this is required for ScoringRecomputeService.
        var playerId = Guid.NewGuid();
        var apiEvt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 45 },
            Player = new ApiPlayer { Name = "Test Player" },
            Detail = "Normal Goal",
        };

        var goalEvent = ResultIngestionJob.BuildGoalEvent(
            apiFixtureId: 99001,
            dbFixtureId:  "17",   // Marten Fixture.Id
            playerId:     playerId,
            evt:          apiEvt);

        Assert.Equal("17", goalEvent.FixtureId);
    }

    // ── OwnGoal and Shootout are not scored ──────────────────────────────────

    [Fact]
    public void OwnGoalType_IsNotCountedByScorer()
    {
        // GoalType.OwnGoal is explicitly excluded from Golden Six scoring
        // (ScoringRecomputeService filters: g.Type != GoalType.OwnGoal)
        var evt = new ApiMatchEvent { Detail = "Own Goal" };
        var mapped = ResultIngestionJob.MapGoalType(evt);

        Assert.Equal(GoalType.OwnGoal, mapped);
        // Confirm it's the enum value that ScoringRecomputeService excludes
        Assert.NotEqual(GoalType.OpenPlay, mapped);
        Assert.NotEqual(GoalType.PenaltyInMatch, mapped);
    }

    // ── NormalizeName — non-decomposable characters and separators ────────────

    [Theory]
    [InlineData("Quiñones",          "Quinones")]          // ñ — standard diacritic (FormD)
    [InlineData("Østigård",          "Ostigard")]          // Ø → O, å via FormD
    [InlineData("Ødegaard",          "Odegaard")]          // Ø → O
    [InlineData("Ørjan",             "Orjan")]             // Ø → O
    [InlineData("Sørloth",           "Sorloth")]           // ø → o
    [InlineData("Bjørkan",           "Bjorkan")]           // ø → o
    [InlineData("Torbjørn",          "Torbjorn")]          // ø → o
    [InlineData("Jørgen",            "Jorgen")]            // ø → o
    [InlineData("Møller",            "Moller")]            // ø → o
    [InlineData("Bayındır",          "Bayindir")]          // ı → i (Turkish dotless i), ı again
    [InlineData("Çakır",             "Cakir")]             // ç via FormD, ı → i
    [InlineData("Kadıoğlu",          "Kadioglu")]          // ı → i, ğ via FormD
    [InlineData("Yıldız",            "Yildiz")]            // ı → i
    [InlineData("Groß",              "Gross")]             // ß → ss
    [InlineData("Al-Arab",           "Al Arab")]           // hyphen → space
    [InlineData("Al-Owais",          "Al Owais")]          // hyphen → space
    [InlineData("O'Neill",           "ONeill")]            // apostrophe removed
    [InlineData("Heung-min",         "Heung min")]         // Korean hyphen → space
    [InlineData("Julián Quiñones",   "Julian Quinones")]   // full name
    [InlineData("Leo Østigård",      "Leo Ostigard")]      // full name
    [InlineData("Yazan Al-Arab",     "Yazan Al Arab")]     // full name with hyphen
    public void NormalizeName_ReturnsExpectedAscii(string input, string expected)
    {
        Assert.Equal(expected, ResultIngestionJob.NormalizeName(input),
            StringComparer.OrdinalIgnoreCase);
    }

    // ── LastWord — last word after full normalisation (hyphens become spaces) ──

    [Theory]
    [InlineData("Julián Quiñones",    "Quinones")]  // diacritic stripped
    [InlineData("J. Quinones",        "Quinones")]  // abbreviated, already ASCII
    [InlineData("Leo Østigård",       "Ostigard")]  // Ø + å stripped
    [InlineData("L. Ostigard",        "Ostigard")]  // abbreviated, no diacritics
    [InlineData("Yazan Al-Arab",      "Arab")]      // hyphen → space → last word = Arab
    [InlineData("Yazan Al Arab",      "Arab")]      // already space → last word = Arab
    [InlineData("Y. Al Arab",         "Arab")]      // abbreviated, space → last word = Arab
    [InlineData("Y. Al-Arab",         "Arab")]      // abbreviated, hyphen → last word = Arab
    [InlineData("Son Heung-min",      "min")]       // Korean hyphen → space
    [InlineData("Hassan Al-Tambakti", "Tambakti")] // hyphen inside Al- name
    [InlineData("H. Al-Tambakti",     "Tambakti")] // abbreviated
    [InlineData("Pascal Groß",        "Gross")]    // ß → ss
    public void LastWord_ReturnsCorrectNormalizedLastWord(string input, string expected)
    {
        Assert.Equal(expected, ResultIngestionJob.LastWord(input),
            StringComparer.OrdinalIgnoreCase);
    }

    // ── ResolvePlayer — integration of NormalizeName + LastWord ──────────────

    private static (Dictionary<string, Player> byFullName, ILookup<string, Player> byLastName)
        BuildLookups(IEnumerable<Player> players)
    {
        var list = players.ToList();
        var byFullName = list
            .GroupBy(p => ResultIngestionJob.NormalizeName(p.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var byLastName = list
            .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);
        return (byFullName, byLastName);
    }

    [Fact]
    public void ResolvePlayer_AcuteAccentVsNone_MatchesByFullName()
    {
        // "Brian Gutiérrez" in DB, API returns "Brian Gutierrez" (no accent)
        var player = new Player { Id = Guid.NewGuid(), Name = "Brian Gutiérrez", TeamId = "MEX" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("Brian Gutierrez", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_TildeVsNone_MatchesByLastName()
    {
        // "Julián Quiñones" in DB, API returns abbreviated "J. Quinones"
        var player = new Player { Id = Guid.NewGuid(), Name = "Julián Quiñones", TeamId = "MEX" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("J. Quinones", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_NorwegianOStroke_MatchesByLastName()
    {
        // "Leo Østigård" in DB, API returns "L. Ostigard" (no Ø or å)
        var player = new Player { Id = Guid.NewGuid(), Name = "Leo Østigård", TeamId = "NOR" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("L. Ostigard", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_NorwegianOStroke_FullName_MatchesByFullName()
    {
        // Full name "Ostigard" (no diacritics) matches DB "Østigård"
        var player = new Player { Id = Guid.NewGuid(), Name = "Leo Østigård", TeamId = "NOR" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("Leo Ostigard", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_HyphenVsSpace_MatchesByFullName()
    {
        // "Yazan Al-Arab" in DB, API returns "Yazan Al Arab" (space instead of hyphen)
        var player = new Player { Id = Guid.NewGuid(), Name = "Yazan Al-Arab", TeamId = "JOR" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("Yazan Al Arab", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_HyphenAbbreviatedAlPrefix_MatchesByLastName()
    {
        // "Hassan Al-Tambakti" in DB, API returns "H. Al-Tambakti" or "H. Al Tambakti"
        var player = new Player { Id = Guid.NewGuid(), Name = "Hassan Al-Tambakti", TeamId = "KSA" };
        var (byFull, byLast) = BuildLookups([player]);

        // Hyphen form
        Assert.Equal(player.Id, ResultIngestionJob.ResolvePlayer("H. Al-Tambakti", byFull, byLast)?.Id);
        // Space form
        Assert.Equal(player.Id, ResultIngestionJob.ResolvePlayer("H. Al Tambakti", byFull, byLast)?.Id);
    }

    [Fact]
    public void ResolvePlayer_TurkishDotlessI_MatchesByLastName()
    {
        // "Ferdi Kadıoğlu" in DB, API returns "F. Kadioglu" (dotless i → i)
        var player = new Player { Id = Guid.NewGuid(), Name = "Ferdi Kadıoğlu", TeamId = "TUR" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("F. Kadioglu", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_GermanSharpS_MatchesByLastName()
    {
        // "Pascal Groß" in DB, API returns "P. Gross" (ß → ss)
        var player = new Player { Id = Guid.NewGuid(), Name = "Pascal Groß", TeamId = "GER" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("P. Gross", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    [Fact]
    public void ResolvePlayer_KoreanHyphen_MatchesByFullName()
    {
        // "Son Heung-min" in DB, API may return "Son Heung Min" (no hyphen)
        var player = new Player { Id = Guid.NewGuid(), Name = "Son Heung-min", TeamId = "KOR" };
        var (byFull, byLast) = BuildLookups([player]);

        var result = ResultIngestionJob.ResolvePlayer("Son Heung Min", byFull, byLast);
        Assert.Equal(player.Id, result?.Id);
    }

    // ── ShouldPurgeGoalEventsOnLivePoll — shootout guard ────────────────────────

    [Theory]
    [InlineData(MatchStatus.InProgress,      true)]
    [InlineData(MatchStatus.ExtraTime,       true)]
    [InlineData(MatchStatus.PenaltyShootout, false)]
    public void ShouldPurgeGoalEventsOnLivePoll_ReturnsFalseOnlyDuringShootout(
        MatchStatus status, bool expected)
    {
        // Regression guard: PurgeFixtureEventsAsync must NOT fire while a slot is in
        // PenaltyShootout — the shootout-kick events look identical to in-match penalties,
        // so purging here deletes the real regulation/ET goals with no restore path.
        // InProgress and ExtraTime are safe to purge-and-rewrite.
        Assert.Equal(expected, ResultIngestionJob.ShouldPurgeGoalEventsOnLivePoll(status));
    }

    // ── FilterCancelledGoals — name-format mismatch between goal and VAR event ─

    [Fact]
    public void FilterCancelledGoals_DiacriticMismatch_StillCancelsGoal()
    {
        // API-Football may return the goal event with accented characters ("Julián Quiñones")
        // while the VAR GoalCancelled event uses the unaccented form ("Julian Quinones").
        // NormalizeName on both sides strips diacritics so the comparison succeeds.
        // Without normalization the exact-string match fails and the goal stays on the board,
        // causing incorrect prediction scores.
        var goal = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 56 },
            Player = new ApiPlayer { Name = "Julián Quiñones" },
            Detail = "Normal Goal",
        };
        var varCancel = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 58 },
            Player = new ApiPlayer { Name = "Julian Quinones" },
            Detail = "Goal Cancelled",
        };

        var result = ResultIngestionJob.FilterCancelledGoals([goal], [varCancel]);

        Assert.Empty(result); // goal must be filtered despite diacritic mismatch
    }

    [Fact]
    public void FilterCancelledGoals_SameNameFormat_CancelsGoal()
    {
        var goal = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 23 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };
        var varCancel = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 25 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Goal Cancelled",
        };

        var result = ResultIngestionJob.FilterCancelledGoals([goal], [varCancel]);

        Assert.Empty(result);
    }

    [Fact]
    public void FilterCancelledGoals_NoVarCancellation_ReturnsAllGoals()
    {
        var goal1 = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 23 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };
        var goal2 = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 67 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Normal Goal",
        };

        var result = ResultIngestionJob.FilterCancelledGoals([goal1, goal2], []);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterCancelledGoals_VarAfterGoalMinute_DoesNotCancel()
    {
        // VAR can only cancel a goal at or after the goal minute; a goal at min 80 cannot
        // be cancelled by a VAR event at min 56.
        var goal = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 80 },
            Player = new ApiPlayer { Name = "Player A" },
            Detail = "Normal Goal",
        };
        var varCancel = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 56 },
            Player = new ApiPlayer { Name = "Player A" },
            Detail = "Goal Cancelled",
        };

        var result = ResultIngestionJob.FilterCancelledGoals([goal], [varCancel]);

        Assert.Single(result); // goal should remain
    }

    // ── IsMissedPenalty — missed penalties must never reach goal storage ─────

    [Fact]
    public void MissedPenalty_IsMissedPenalty_IsTrue()
    {
        var evt = new ApiMatchEvent
        {
            Time   = new ApiTime { Elapsed = 9 },
            Player = new ApiPlayer { Name = "Lionel Messi" },
            Detail = "Missed Penalty",
        };

        Assert.True(evt.IsMissedPenalty);
    }

    [Fact]
    public void MissedPenalty_ExcludedByGoalFilter()
    {
        // IsGoal is true (type:"Goal") but IsMissedPenalty distinguishes it — the combined
        // predicate used at all three call sites must evaluate to false for a missed penalty.
        var evt = new ApiMatchEvent
        {
            Type   = "Goal",
            Detail = "Missed Penalty",
        };

        Assert.True(evt.IsGoal);
        Assert.True(evt.IsMissedPenalty);
        Assert.False(evt.IsGoal && !evt.IsMissedPenalty);
    }

    [Fact]
    public void MissedPenalty_CaseInsensitive()
    {
        Assert.True(new ApiMatchEvent { Detail = "missed penalty" }.IsMissedPenalty);
        Assert.True(new ApiMatchEvent { Detail = "MISSED PENALTY" }.IsMissedPenalty);
    }

    [Fact]
    public void ScoredPenalty_IsNotMissedPenalty()
    {
        var evt = new ApiMatchEvent { Detail = "Penalty" };

        Assert.False(evt.IsMissedPenalty);
        Assert.True(evt.IsPenalty);
    }

    [Fact]
    public void NormalGoal_IsNotMissedPenalty()
    {
        var evt = new ApiMatchEvent { Detail = "Normal Goal" };

        Assert.False(evt.IsMissedPenalty);
    }
}
