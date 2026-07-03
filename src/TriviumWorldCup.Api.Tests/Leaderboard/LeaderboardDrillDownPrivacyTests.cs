using TriviumWorldCup.Api.Leaderboard;
using TriviumWorldCup.Api.Predictions;

namespace TriviumWorldCup.Api.Tests.Leaderboard;

/// <summary>
/// TWC-61: GET /leaderboard/{userId} must gate champion + Golden Six behind
/// isSelf || tournamentLocked, matching the "reveal once locked/completed" rule already
/// applied to group and knockout predictions in the same drill-down response.
/// Pure predicate — no database required.
/// </summary>
public class LeaderboardDrillDownPrivacyTests
{
    [Fact]
    public void ShouldReveal_Self_AlwaysTrue_RegardlessOfLock()
    {
        Assert.True(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: true, tournamentLocked: false));
        Assert.True(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: true, tournamentLocked: true));
    }

    [Fact]
    public void ShouldReveal_OtherMember_PreLock_ReturnsFalse()
    {
        // Viewer != target, tournament not yet locked → champion/Golden Six must be hidden.
        Assert.False(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: false, tournamentLocked: false));
    }

    [Fact]
    public void ShouldReveal_OtherMember_PostLock_ReturnsTrue()
    {
        Assert.True(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: false, tournamentLocked: true));
    }

    // ── Tournament lock reuses TournamentPredictionValidator.IsLocked (same as
    //    POST/PUT /predictions/tournament) — earliest fixture kickoff, or immediately once
    //    any fixture is completed. Verified here end-to-end with the drill-down gate. ──

    [Fact]
    public void OtherMember_BeforeFirstKickoff_TournamentPredictionHidden()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 11, 18, 0, 0, TimeSpan.Zero);
        var locked = TournamentPredictionValidator.IsLocked(firstKickoff, now);

        Assert.False(locked);
        Assert.False(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: false, tournamentLocked: locked));
    }

    [Fact]
    public void OtherMember_AfterFirstKickoff_TournamentPredictionRevealed()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 11, 19, 0, 1, TimeSpan.Zero);
        var locked = TournamentPredictionValidator.IsLocked(firstKickoff, now);

        Assert.True(locked);
        Assert.True(LeaderboardEndpoints.ShouldRevealTournamentPrediction(isSelf: false, tournamentLocked: locked));
    }
}
