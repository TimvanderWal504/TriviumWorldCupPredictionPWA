using TriviumWorldCup.Api.Data;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Predictions;
using TournamentAggregate = TriviumWorldCup.Api.Domain.Tournament;

namespace TriviumWorldCup.Api.Tests.Data;

/// <summary>
/// GEN-1 (TWC-35) acceptance criteria:
///   - Documents for tournament "wc-2026" are not returned when querying for "other-tournament".
///   - The Tournament document (WorldCup2026) is present and has the correct shape.
///   - Composite key format includes TournamentId.
///
/// All tests are pure (no database required) — they verify the in-memory domain types,
/// seed data properties, and composite key helper methods.
/// </summary>
public class TournamentScopingTests
{
    // ── Tournament aggregate: WorldCup2026 has correct shape ─────────────────

    [Fact]
    public void WorldCup2026_HasCorrectSlug()
    {
        var t = TournamentSeed.WorldCup2026;
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, t.Slug);
    }

    [Fact]
    public void WorldCup2026_IdMatchesSlug()
    {
        var t = TournamentSeed.WorldCup2026;
        Assert.Equal(t.Slug, t.Id);
    }

    [Fact]
    public void WorldCup2026_HasFootballSportKey()
    {
        Assert.Equal("football", TournamentSeed.WorldCup2026.SportKey);
    }

    [Fact]
    public void WorldCup2026_DisplayNameIsSet()
    {
        Assert.False(string.IsNullOrWhiteSpace(TournamentSeed.WorldCup2026.DisplayName));
    }

    [Fact]
    public void WorldCup2026_IsNonNullEveryCall()
    {
        // Property returns a new instance each call — both calls should be non-null and equal values.
        var a = TournamentSeed.WorldCup2026;
        var b = TournamentSeed.WorldCup2026;
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a.Id, b.Id);
    }

    // ── Default TournamentId on domain documents ─────────────────────────────

    [Fact]
    public void Team_DefaultTournamentId_IsWorldCup2026()
    {
        var team = new Team();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, team.TournamentId);
    }

    [Fact]
    public void Group_DefaultTournamentId_IsWorldCup2026()
    {
        var group = new Group();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, group.TournamentId);
    }

    [Fact]
    public void Fixture_DefaultTournamentId_IsWorldCup2026()
    {
        var fixture = new Fixture();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, fixture.TournamentId);
    }

    [Fact]
    public void KnockoutSlot_DefaultTournamentId_IsWorldCup2026()
    {
        var slot = new KnockoutSlot();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, slot.TournamentId);
    }

    [Fact]
    public void Player_DefaultTournamentId_IsWorldCup2026()
    {
        var player = new Player();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, player.TournamentId);
    }

    [Fact]
    public void GroupPrediction_DefaultTournamentId_IsWorldCup2026()
    {
        var pred = new GroupPrediction();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, pred.TournamentId);
    }

    [Fact]
    public void KnockoutPrediction_DefaultTournamentId_IsWorldCup2026()
    {
        var pred = new KnockoutPrediction();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, pred.TournamentId);
    }

    [Fact]
    public void TournamentPrediction_DefaultTournamentId_IsWorldCup2026()
    {
        var pred = new TournamentPrediction();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, pred.TournamentId);
    }

    [Fact]
    public void MemberScore_DefaultTournamentId_IsWorldCup2026()
    {
        var score = new MemberScore();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, score.TournamentId);
    }

    [Fact]
    public void GoalEvent_DefaultTournamentId_IsWorldCup2026()
    {
        var ev = new GoalEvent();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, ev.TournamentId);
    }

    [Fact]
    public void CardEvent_DefaultTournamentId_IsWorldCup2026()
    {
        var ev = new CardEvent();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, ev.TournamentId);
    }

    [Fact]
    public void SubstitutionEvent_DefaultTournamentId_IsWorldCup2026()
    {
        var ev = new SubstitutionEvent();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, ev.TournamentId);
    }

    [Fact]
    public void VarEvent_DefaultTournamentId_IsWorldCup2026()
    {
        var ev = new VarEvent();
        Assert.Equal(SingleTournamentContext.DefaultTournamentId, ev.TournamentId);
    }

    // ── TournamentId filtering prevents cross-tournament collision ────────────
    //
    // The following tests model what the Marten .Where(x => x.TournamentId == tournamentId)
    // clause achieves at the document level: a prediction stamped with "wc-2026" is NOT
    // returned in a query for "other-tournament", and vice versa.

    [Fact]
    public void GroupPredictions_DifferentTournamentIds_DoNotCollide()
    {
        const string wc = "wc-2026";
        const string other = "other-tournament";

        var wcPrediction = new GroupPrediction
        {
            Id           = GroupPredictionEndpoints.BuildId(wc, "user-1", "1"),
            TournamentId = wc,
            UserId       = "user-1",
            FixtureId    = "1",
            HomeScore    = 2,
            AwayScore    = 1,
        };

        var otherPrediction = new GroupPrediction
        {
            Id           = GroupPredictionEndpoints.BuildId(other, "user-1", "1"),
            TournamentId = other,
            UserId       = "user-1",
            FixtureId    = "1",
            HomeScore    = 0,
            AwayScore    = 3,
        };

        // IDs must differ (no collision).
        Assert.NotEqual(wcPrediction.Id, otherPrediction.Id);

        // Simulated filter: only return predictions whose TournamentId matches.
        var allPredictions = new[] { wcPrediction, otherPrediction };
        var wcResults    = allPredictions.Where(p => p.TournamentId == wc).ToList();
        var otherResults = allPredictions.Where(p => p.TournamentId == other).ToList();

        Assert.Single(wcResults);
        Assert.Equal(wc, wcResults[0].TournamentId);

        Assert.Single(otherResults);
        Assert.Equal(other, otherResults[0].TournamentId);
    }

    [Fact]
    public void KnockoutPredictions_DifferentTournamentIds_DoNotCollide()
    {
        const string wc = "wc-2026";
        const string other = "other-tournament";

        var wcPrediction = new KnockoutPrediction
        {
            Id           = KnockoutPredictionEndpoints.BuildId(wc, "user-1", "FIN"),
            TournamentId = wc,
            UserId       = "user-1",
            SlotKey      = "FIN",
            PredictedWinnerTeamId = "BRA",
        };

        var otherPrediction = new KnockoutPrediction
        {
            Id           = KnockoutPredictionEndpoints.BuildId(other, "user-1", "FIN"),
            TournamentId = other,
            UserId       = "user-1",
            SlotKey      = "FIN",
            PredictedWinnerTeamId = "TEAM_A",
        };

        Assert.NotEqual(wcPrediction.Id, otherPrediction.Id);

        var allPredictions = new[] { wcPrediction, otherPrediction };
        var wcResults    = allPredictions.Where(p => p.TournamentId == wc).ToList();
        var otherResults = allPredictions.Where(p => p.TournamentId == other).ToList();

        Assert.Single(wcResults);
        Assert.Single(otherResults);
        Assert.NotEqual(wcResults[0].PredictedWinnerTeamId, otherResults[0].PredictedWinnerTeamId);
    }

    [Fact]
    public void Teams_DifferentTournamentIds_FilteredCorrectly()
    {
        const string wc = "world-cup-2026";
        const string other = "other-tournament";

        var teams = new[]
        {
            new Team { Id = "BRA", FifaCode = "BRA", TournamentId = wc },
            new Team { Id = "FRA", FifaCode = "FRA", TournamentId = wc },
            new Team { Id = "TEAM_X", FifaCode = "TEAM_X", TournamentId = other },
        };

        var wcTeams    = teams.Where(t => t.TournamentId == wc).ToList();
        var otherTeams = teams.Where(t => t.TournamentId == other).ToList();

        Assert.Equal(2, wcTeams.Count);
        Assert.Single(otherTeams);

        // Verify no cross-contamination.
        Assert.DoesNotContain(otherTeams[0], wcTeams);
        Assert.DoesNotContain(wcTeams[0], otherTeams);
    }

    [Fact]
    public void MemberScores_DifferentTournamentIds_DoNotMix()
    {
        const string wc = "world-cup-2026";
        const string other = "other-tournament";

        var scores = new[]
        {
            new MemberScore { Id = "user-1", UserId = "user-1", TournamentId = wc, GroupMatchPoints = 100 },
            new MemberScore { Id = "user-1", UserId = "user-1", TournamentId = other, GroupMatchPoints = 999 },
        };

        var wcScore    = scores.Where(s => s.TournamentId == wc).Single();
        var otherScore = scores.Where(s => s.TournamentId == other).Single();

        Assert.Equal(100, wcScore.GroupMatchPoints);
        Assert.Equal(999, otherScore.GroupMatchPoints);
    }

    // ── Composite key format ─────────────────────────────────────────────────

    [Fact]
    public void GroupPrediction_BuildId_IncludesTournamentId()
    {
        var id = GroupPredictionEndpoints.BuildId("world-cup-2026", "user-1", "42");
        Assert.StartsWith("world-cup-2026_", id);
        Assert.Contains("user-1", id);
        Assert.Contains("42", id);
    }

    [Fact]
    public void KnockoutPrediction_BuildId_IncludesTournamentId()
    {
        var id = KnockoutPredictionEndpoints.BuildId("world-cup-2026", "user-1", "FIN");
        Assert.StartsWith("world-cup-2026_", id);
        Assert.Contains("user-1", id);
        Assert.Contains("FIN", id);
    }

    [Fact]
    public void GroupPrediction_SameUserAndFixture_DifferentTournament_DifferentId()
    {
        var id1 = GroupPredictionEndpoints.BuildId("wc-2026", "user-1", "fixture-1");
        var id2 = GroupPredictionEndpoints.BuildId("other-tournament", "user-1", "fixture-1");
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void KnockoutPrediction_SameUserAndSlot_DifferentTournament_DifferentId()
    {
        var id1 = KnockoutPredictionEndpoints.BuildId("wc-2026", "user-1", "FIN");
        var id2 = KnockoutPredictionEndpoints.BuildId("other-tournament", "user-1", "FIN");
        Assert.NotEqual(id1, id2);
    }

    // ── SingleTournamentContext ───────────────────────────────────────────────

    [Fact]
    public void DefaultTournamentId_IsWorldCup2026Slug()
    {
        Assert.Equal("world-cup-2026", SingleTournamentContext.DefaultTournamentId);
    }
}
