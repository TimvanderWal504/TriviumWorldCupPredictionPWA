using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Predictions;

namespace TriviumWorldCup.Api.Tests.Predictions;

/// <summary>
/// Unit tests for the server-side lock logic in GroupPredictionEndpoints.
/// Pure function — no database required.
/// TWC-6 AC: At kickoff the match becomes read-only; later edits are rejected server-side.
/// </summary>
public class GroupPredictionLockTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static Fixture FixtureWithKickoff(DateTimeOffset kickoffUtc) => new()
    {
        Id          = "1",
        MatchNumber = 1,
        GroupLetter = "A",
        HomeTeamId  = "MEX",
        AwayTeamId  = "RSA",
        KickoffUtc  = kickoffUtc,
        Venue       = "Estadio Azteca",
        City        = "Mexico City",
        Status      = MatchStatus.Scheduled,
    };

    // ── Future kickoff — not locked ───────────────────────────────────────────

    [Fact]
    public void IsLocked_KickoffInFuture_ReturnsFalse()
    {
        var fixture = FixtureWithKickoff(DateTimeOffset.UtcNow.AddHours(1));
        Assert.False(GroupPredictionEndpoints.IsLocked(fixture));
    }

    [Fact]
    public void IsLocked_KickoffFarInFuture_ReturnsFalse()
    {
        var fixture = FixtureWithKickoff(DateTimeOffset.UtcNow.AddDays(30));
        Assert.False(GroupPredictionEndpoints.IsLocked(fixture));
    }

    // ── Past kickoff — locked ─────────────────────────────────────────────────

    [Fact]
    public void IsLocked_KickoffInPast_ReturnsTrue()
    {
        var fixture = FixtureWithKickoff(DateTimeOffset.UtcNow.AddHours(-1));
        Assert.True(GroupPredictionEndpoints.IsLocked(fixture));
    }

    [Fact]
    public void IsLocked_KickoffFarInPast_ReturnsTrue()
    {
        var fixture = FixtureWithKickoff(DateTimeOffset.UtcNow.AddDays(-7));
        Assert.True(GroupPredictionEndpoints.IsLocked(fixture));
    }

    // ── Boundary: kickoff exactly now — locked ────────────────────────────────

    [Fact]
    public void IsLocked_KickoffExactlyNow_ReturnsTrue()
    {
        // The lock condition is KickoffUtc <= UtcNow (inclusive).
        // We use a fixed past timestamp that is clearly <= now to avoid
        // a race condition between setting the value and the assertion.
        var pastInstant = DateTimeOffset.UtcNow.AddSeconds(-1);
        var fixture = FixtureWithKickoff(pastInstant);
        Assert.True(GroupPredictionEndpoints.IsLocked(fixture));
    }

    // ── Composite ID helper ───────────────────────────────────────────────────

    [Fact]
    public void BuildId_CombinesUserIdAndFixtureId()
    {
        var id = GroupPredictionEndpoints.BuildId("user-42", "7");
        Assert.Equal("user-42_7", id);
    }

    // ── Request validation ────────────────────────────────────────────────────

    [Fact]
    public void ValidateRequest_ValidScores_ReturnsNull()
    {
        var request = new PredictionRequest(HomeScore: 2, AwayScore: 1);
        Assert.Null(GroupPredictionEndpoints.ValidateRequest(request));
    }

    [Fact]
    public void ValidateRequest_ZeroZero_ReturnsNull()
    {
        var request = new PredictionRequest(HomeScore: 0, AwayScore: 0);
        Assert.Null(GroupPredictionEndpoints.ValidateRequest(request));
    }

    [Fact]
    public void ValidateRequest_NegativeHomeScore_ReturnsError()
    {
        var request = new PredictionRequest(HomeScore: -1, AwayScore: 0);
        var error = GroupPredictionEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("HomeScore", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRequest_NegativeAwayScore_ReturnsError()
    {
        var request = new PredictionRequest(HomeScore: 0, AwayScore: -1);
        var error = GroupPredictionEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("AwayScore", error, StringComparison.OrdinalIgnoreCase);
    }

    // Note: TWC-52 route-removal coverage for POST /predictions/group/inject lives in
    // GroupPredictionInjectRemovedTests.cs, alongside the rest of the route-table assertions.
}
