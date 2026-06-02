using TriviumWorldCup.Api.Predictions;

namespace TriviumWorldCup.Api.Tests.Predictions;

/// <summary>
/// Unit tests for tournament prediction logic — pure logic, no database required.
/// Covers TWC-7 acceptance criteria: lock time, validation, champion validation.
/// </summary>
public class TournamentPredictionTests
{
    // ── Lock time: IsLocked ───────────────────────────────────────────────────

    [Fact]
    public void IsLocked_BeforeFirstKickoff_ReturnsFalse()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 11, 18, 59, 59, TimeSpan.Zero);
        Assert.False(TournamentPredictionValidator.IsLocked(firstKickoff, now));
    }

    [Fact]
    public void IsLocked_ExactlyAtFirstKickoff_ReturnsTrue()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        Assert.True(TournamentPredictionValidator.IsLocked(firstKickoff, now));
    }

    [Fact]
    public void IsLocked_OneSecondAfterFirstKickoff_ReturnsTrue()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 11, 19, 0, 1, TimeSpan.Zero);
        Assert.True(TournamentPredictionValidator.IsLocked(firstKickoff, now));
    }

    [Fact]
    public void IsLocked_WellAfterFirstKickoff_ReturnsTrue()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero); // final
        Assert.True(TournamentPredictionValidator.IsLocked(firstKickoff, now));
    }

    [Fact]
    public void IsLocked_OneDayBeforeFirstKickoff_ReturnsFalse()
    {
        var firstKickoff = new DateTimeOffset(2026, 6, 11, 19, 0, 0, TimeSpan.Zero);
        var now          = new DateTimeOffset(2026, 6, 10, 19, 0, 0, TimeSpan.Zero);
        Assert.False(TournamentPredictionValidator.IsLocked(firstKickoff, now));
    }

    // ── Golden Six count validation ───────────────────────────────────────────

    [Fact]
    public void ValidateGoldenSixCount_ExactlySix_ReturnsNull()
    {
        var ids = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        Assert.Null(TournamentPredictionValidator.ValidateGoldenSixCount(ids));
    }

    [Fact]
    public void ValidateGoldenSixCount_Five_ReturnsError()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var error = TournamentPredictionValidator.ValidateGoldenSixCount(ids);
        Assert.NotNull(error);
        Assert.Contains("6", error);
    }

    [Fact]
    public void ValidateGoldenSixCount_Seven_ReturnsError()
    {
        var ids = Enumerable.Range(0, 7).Select(_ => Guid.NewGuid()).ToList();
        var error = TournamentPredictionValidator.ValidateGoldenSixCount(ids);
        Assert.NotNull(error);
        Assert.Contains("6", error);
    }

    [Fact]
    public void ValidateGoldenSixCount_Zero_ReturnsError()
    {
        var ids = new List<Guid>();
        var error = TournamentPredictionValidator.ValidateGoldenSixCount(ids);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateGoldenSixCount_Null_ReturnsError()
    {
        var error = TournamentPredictionValidator.ValidateGoldenSixCount(null);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateGoldenSixCount_SixDuplicates_ReturnsNull()
    {
        // Duplicate player IDs within one member's picks are allowed by the ACs:
        // "two members may hold identical picks" — no uniqueness restriction on the picks themselves.
        var sameId = Guid.NewGuid();
        var ids = Enumerable.Repeat(sameId, 6).ToList();
        Assert.Null(TournamentPredictionValidator.ValidateGoldenSixCount(ids));
    }

    // ── Champion team ID validation ───────────────────────────────────────────

    [Fact]
    public void ValidateChampionTeamId_ValidCode_ReturnsNull()
    {
        Assert.Null(TournamentPredictionValidator.ValidateChampionTeamId("ARG"));
    }

    [Fact]
    public void ValidateChampionTeamId_EmptyString_ReturnsError()
    {
        var error = TournamentPredictionValidator.ValidateChampionTeamId("");
        Assert.NotNull(error);
        Assert.Contains("ChampionTeamId", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateChampionTeamId_WhitespaceOnly_ReturnsError()
    {
        var error = TournamentPredictionValidator.ValidateChampionTeamId("   ");
        Assert.NotNull(error);
        Assert.Contains("ChampionTeamId", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateChampionTeamId_Null_ReturnsError()
    {
        var error = TournamentPredictionValidator.ValidateChampionTeamId(null);
        Assert.NotNull(error);
        Assert.Contains("ChampionTeamId", error, StringComparison.OrdinalIgnoreCase);
    }
}
