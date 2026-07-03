using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Predictions;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Predictions;

/// <summary>
/// Unit tests for the server-side lock and bracket-progression logic in KnockoutPredictionEndpoints.
/// Pure function — no database required.
/// TWC-14 AC: locks at kickoff; teams-not-determined check; bracket progression enforcement.
/// </summary>
public class KnockoutPredictionTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static KnockoutSlot MakeSlot(
        string slotKey = "QF-1",
        string? homeTeamId = "BRA",
        string? awayTeamId = "ARG",
        DateTimeOffset? kickoffUtc = null)
    {
        return new KnockoutSlot
        {
            Id          = slotKey,
            SlotKey     = slotKey,
            Round       = Round.QF,
            SlotNumber  = 1,
            HomeTeamId  = homeTeamId,
            AwayTeamId  = awayTeamId,
            KickoffUtc  = kickoffUtc ?? DateTimeOffset.UtcNow.AddDays(10),
            HomeSlotSource = new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R16-1" },
            AwaySlotSource = new SlotSource { Type = SlotSourceType.MatchWinner, Reference = "R16-2" },
        };
    }

    // ── IsLocked: future kickoff → not locked ─────────────────────────────────

    [Fact]
    public void IsLocked_KickoffInFuture_ReturnsFalse()
    {
        var slot = MakeSlot(kickoffUtc: DateTimeOffset.UtcNow.AddHours(1));
        Assert.False(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    [Fact]
    public void IsLocked_KickoffFarInFuture_ReturnsFalse()
    {
        var slot = MakeSlot(kickoffUtc: DateTimeOffset.UtcNow.AddDays(30));
        Assert.False(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    // ── IsLocked: past kickoff → locked ───────────────────────────────────────

    [Fact]
    public void IsLocked_KickoffInPast_ReturnsTrue()
    {
        var slot = MakeSlot(kickoffUtc: DateTimeOffset.UtcNow.AddHours(-1));
        Assert.True(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    [Fact]
    public void IsLocked_KickoffFarInPast_ReturnsTrue()
    {
        var slot = MakeSlot(kickoffUtc: DateTimeOffset.UtcNow.AddDays(-7));
        Assert.True(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    [Fact]
    public void IsLocked_KickoffJustPassed_ReturnsTrue()
    {
        var slot = MakeSlot(kickoffUtc: DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.True(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    // ── IsLocked: null kickoff → locked (safe default) ────────────────────────

    [Fact]
    public void IsLocked_NullKickoff_ReturnsTrue()
    {
        var slot = MakeSlot();
        slot.KickoffUtc = null;
        Assert.True(KnockoutPredictionEndpoints.IsLocked(slot));
    }

    // ── ValidateWinner: slot teams not set ────────────────────────────────────
    // (The endpoint returns 422 before reaching ValidateWinner when teams are null,
    //  but we also guard ValidateWinner itself against null — test endpoint-layer separately.)

    [Fact]
    public void ValidateWinner_EmptyString_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner(string.Empty, slot);
        Assert.NotNull(error);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateWinner_WhitespaceOnly_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("   ", slot);
        Assert.NotNull(error);
    }

    // ── ValidateWinner: winner not matching either team → rejected ────────────

    [Fact]
    public void ValidateWinner_ThirdTeam_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("FRA", slot);
        Assert.NotNull(error);
        Assert.Contains("BRA", error);
        Assert.Contains("ARG", error);
    }

    [Fact]
    public void ValidateWinner_RandomString_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "ENG", awayTeamId: "GER");
        var error = KnockoutPredictionEndpoints.ValidateWinner("USA", slot);
        Assert.NotNull(error);
    }

    // ── ValidateWinner: matching HomeTeamId → accepted ────────────────────────

    [Fact]
    public void ValidateWinner_MatchingHomeTeamId_ReturnsNull()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("BRA", slot);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateWinner_MatchingHomeTeamIdLowerCase_ReturnsNull()
    {
        // Case-insensitive match
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("bra", slot);
        Assert.Null(error);
    }

    // ── ValidateWinner: matching AwayTeamId → accepted ────────────────────────

    [Fact]
    public void ValidateWinner_MatchingAwayTeamId_ReturnsNull()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("ARG", slot);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateWinner_MatchingAwayTeamIdLowerCase_ReturnsNull()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidateWinner("arg", slot);
        Assert.Null(error);
    }

    // ── ValidatePrediction: mandatory scores + winner/score consistency ───────

    private static KnockoutPredictionRequest MakeRequest(string winner, int? home, int? away) =>
        new(winner, home, away);

    [Fact]
    public void ValidatePrediction_MissingHomeScore_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", null, 1), slot);
        Assert.NotNull(error);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrediction_MissingAwayScore_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", 2, null), slot);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePrediction_NegativeScore_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", -1, 0), slot);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePrediction_WinnerNotParticipant_ReturnsError()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("FRA", 2, 1), slot);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePrediction_WinnerIsLowerScoringTeam_ReturnsError()
    {
        // Home BRA 1 – 2 ARG away, but winner claimed as BRA → inconsistent
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", 1, 2), slot);
        Assert.NotNull(error);
        Assert.Contains("higher", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrediction_HigherScoringHomeWins_ReturnsNull()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", 2, 1), slot);
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrediction_HigherScoringAwayWins_ReturnsNull()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        var error = KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("ARG", 0, 3), slot);
        Assert.Null(error);
    }

    [Fact]
    public void ValidatePrediction_Tie_EitherTeamMayAdvance()
    {
        // On a level scoreline, either participant is a valid pick (pens / extra time).
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        Assert.Null(KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("BRA", 1, 1), slot));
        Assert.Null(KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("ARG", 1, 1), slot));
    }

    [Fact]
    public void ValidatePrediction_GoallessTie_EitherTeamMayAdvance()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        Assert.Null(KnockoutPredictionEndpoints.ValidatePrediction(MakeRequest("ARG", 0, 0), slot));
    }

    // ── BuildId: composite key format ─────────────────────────────────────────

    [Fact]
    public void BuildId_CombinesUserIdAndSlotKey()
    {
        var id = KnockoutPredictionEndpoints.BuildId("user-42", "QF-1");
        Assert.Equal("user-42_QF-1", id);
    }

    [Fact]
    public void BuildId_SlotWithSpecialKey()
    {
        var id = KnockoutPredictionEndpoints.BuildId("user-1", "FIN");
        Assert.Equal("user-1_FIN", id);
    }

    [Fact]
    public void BuildId_ThirdPlaceSlot()
    {
        var id = KnockoutPredictionEndpoints.BuildId("user-99", "3RD");
        Assert.Equal("user-99_3RD", id);
    }

    // ── Slot with null teams — prediction rejected (422-equivalent guard) ──────
    // These tests verify that the check logic for null teams is distinct from winner validation.
    // The endpoint layer checks HomeTeamId == null || AwayTeamId == null before calling ValidateWinner.

    [Fact]
    public void IsLocked_AndTeamsNull_Scenario_FutureKickoffUnlocked()
    {
        // A slot can be unlocked in time but still not predictable because teams are TBD.
        // The 422 check (teams == null) is separate from the 403 lock check.
        var slot = MakeSlot(homeTeamId: null, awayTeamId: null, kickoffUtc: DateTimeOffset.UtcNow.AddDays(5));
        Assert.False(KnockoutPredictionEndpoints.IsLocked(slot)); // time-lock: not yet locked
        // Teams are null → endpoint would return 422, validated separately from lock
        Assert.Null(slot.HomeTeamId);
        Assert.Null(slot.AwayTeamId);
    }

    [Fact]
    public void IsLocked_AndTeamsKnown_Scenario_FutureKickoff_AcceptsPrediction()
    {
        // Happy path: teams known, future kickoff, winner is one of the two teams.
        var slot = MakeSlot(homeTeamId: "FRA", awayTeamId: "ESP", kickoffUtc: DateTimeOffset.UtcNow.AddDays(3));
        Assert.False(KnockoutPredictionEndpoints.IsLocked(slot));
        Assert.NotNull(slot.HomeTeamId);
        Assert.NotNull(slot.AwayTeamId);
        Assert.Null(KnockoutPredictionEndpoints.ValidateWinner("FRA", slot));
        Assert.Null(KnockoutPredictionEndpoints.ValidateWinner("ESP", slot));
    }

    // ── TWC-58: winner casing normalized to the slot's canonical team ID on store ─────

    [Fact]
    public void CanonicalWinnerTeamId_MatchesHomeTeamIdCaseInsensitively_ReturnsCanonicalHomeTeamId()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        Assert.Equal("BRA", KnockoutPredictionEndpoints.CanonicalWinnerTeamId("bra", slot));
    }

    [Fact]
    public void CanonicalWinnerTeamId_MatchesAwayTeamIdCaseInsensitively_ReturnsCanonicalAwayTeamId()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        Assert.Equal("ARG", KnockoutPredictionEndpoints.CanonicalWinnerTeamId("Arg", slot));
    }

    [Fact]
    public void CanonicalWinnerTeamId_AlreadyCanonical_ReturnsUnchanged()
    {
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");
        Assert.Equal("BRA", KnockoutPredictionEndpoints.CanonicalWinnerTeamId("BRA", slot));
    }

    [Fact]
    public void LowercaseWinnerInput_NormalizedThroughSubmitToScoring_CreditsPointsCorrectly()
    {
        // Regression test for TWC-58: a winner submitted in any casing must persist as the
        // slot's canonical team ID so downstream ordinal scoring comparisons (KnockoutMatchScorer,
        // ScoringRecomputeService, KnockoutStreakCalculator) credit the pick correctly.
        var slot = MakeSlot(homeTeamId: "BRA", awayTeamId: "ARG");

        // User submits the winner in lowercase — request validation accepts it case-insensitively.
        var request = MakeRequest("bra", 2, 1);
        Assert.Null(KnockoutPredictionEndpoints.ValidatePrediction(request, slot));

        // Endpoint normalizes to canonical casing before storing (mirrors POST/PUT handler logic).
        var storedWinnerId = KnockoutPredictionEndpoints.CanonicalWinnerTeamId(request.PredictedWinnerTeamId, slot);
        Assert.Equal("BRA", storedWinnerId);

        // The match result: BRA (home) wins 2-1, recorded as "BRA" (canonical) on the slot.
        var points = KnockoutMatchScorer.Compute(
            predictedWinnerId: storedWinnerId,
            predictedHomeScore: request.PredictedHomeScore,
            predictedAwayScore: request.PredictedAwayScore,
            actualWinnerId: "BRA",
            actualHomeScore: 2,
            actualAwayScore: 1,
            streakBefore: 0);

        // Exact score (10) + advancing team correct (5 × 1) = 15. Would be 10 (score only) if the
        // stored winner ID still carried the raw "bra" casing and failed the ordinal `==` check.
        Assert.Equal(15, points);
    }
}
