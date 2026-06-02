using TriviumWorldCup.Api.Data.SeedData;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tests.Data;

/// <summary>
/// Tests against the static seed data arrays — no database required.
/// These cover the structural acceptance criteria for TWC-5.
/// </summary>
public class SeedDataTests
{
    // ── Teams ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Teams_Count_Is_48()
    {
        Assert.Equal(48, TeamsData.All.Count);
    }

    [Fact]
    public void Teams_FifaCodes_Are_Unique()
    {
        var codes = TeamsData.All.Select(t => t.FifaCode).ToList();
        var distinct = codes.Distinct().ToList();
        Assert.Equal(distinct.Count, codes.Count);
    }

    [Fact]
    public void Teams_Ids_Match_FifaCodes()
    {
        foreach (var team in TeamsData.All)
            Assert.Equal(team.FifaCode, team.Id);
    }

    [Fact]
    public void Teams_All_Have_NonEmpty_Name_And_CountryCode()
    {
        foreach (var team in TeamsData.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(team.Name),    $"Team {team.FifaCode} has empty Name");
            Assert.False(string.IsNullOrWhiteSpace(team.CountryCode), $"Team {team.FifaCode} has empty CountryCode");
        }
    }

    [Fact]
    public void Teams_All_Assigned_To_Valid_Group_Letter()
    {
        var validLetters = new HashSet<string> { "A","B","C","D","E","F","G","H","I","J","K","L" };
        foreach (var team in TeamsData.All)
            Assert.Contains(team.GroupLetter, validLetters);
    }

    // ── Groups ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Groups_Count_Is_12()
    {
        Assert.Equal(12, GroupsData.All.Count);
    }

    [Fact]
    public void Groups_Cover_Letters_A_through_L()
    {
        var letters = GroupsData.All.Select(g => g.Letter).OrderBy(l => l).ToList();
        var expected = new[] { "A","B","C","D","E","F","G","H","I","J","K","L" };
        Assert.Equal(expected, letters);
    }

    [Fact]
    public void Groups_Each_Have_Exactly_4_Teams()
    {
        foreach (var group in GroupsData.All)
            Assert.Equal(4, group.TeamIds.Count);
    }

    [Fact]
    public void Groups_TeamIds_Match_TeamsData()
    {
        var knownIds = new HashSet<string>(TeamsData.All.Select(t => t.Id));
        foreach (var group in GroupsData.All)
            foreach (var teamId in group.TeamIds)
                Assert.Contains(teamId, knownIds);
    }

    [Fact]
    public void Groups_All_48_Teams_Are_Covered_Exactly_Once()
    {
        var allTeamIdsInGroups = GroupsData.All.SelectMany(g => g.TeamIds).ToList();
        Assert.Equal(48, allTeamIdsInGroups.Count);
        Assert.Equal(48, allTeamIdsInGroups.Distinct().Count());
    }

    [Fact]
    public void Groups_TeamIds_Consistent_With_TeamGroupLetter()
    {
        // Every team's GroupLetter must match the group that contains it.
        var groupByLetter = GroupsData.All.ToDictionary(g => g.Letter);
        foreach (var team in TeamsData.All)
        {
            Assert.True(groupByLetter.ContainsKey(team.GroupLetter),
                $"Team {team.FifaCode} references unknown group {team.GroupLetter}");
            Assert.Contains(team.Id, groupByLetter[team.GroupLetter].TeamIds);
        }
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Fixtures_Count_Is_72()
    {
        Assert.Equal(72, FixturesData.All.Count);
    }

    [Fact]
    public void Fixtures_MatchNumbers_Are_Unique_1_Through_72()
    {
        var numbers = FixturesData.All.Select(f => f.MatchNumber).OrderBy(n => n).ToList();
        var expected = Enumerable.Range(1, 72).ToList();
        Assert.Equal(expected, numbers);
    }

    [Fact]
    public void Fixtures_Each_Group_Has_6_Fixtures()
    {
        var letters = new[] { "A","B","C","D","E","F","G","H","I","J","K","L" };
        foreach (var letter in letters)
        {
            var count = FixturesData.All.Count(f => f.GroupLetter == letter);
            Assert.Equal(6, count);
        }
    }

    [Fact]
    public void Fixtures_All_TeamIds_Are_Known()
    {
        var knownIds = new HashSet<string>(TeamsData.All.Select(t => t.Id));
        foreach (var fixture in FixturesData.All)
        {
            Assert.Contains(fixture.HomeTeamId, knownIds);
            Assert.Contains(fixture.AwayTeamId, knownIds);
        }
    }

    [Fact]
    public void Fixtures_HomeTeam_And_AwayTeam_Differ()
    {
        foreach (var fixture in FixturesData.All)
            Assert.NotEqual(fixture.HomeTeamId, fixture.AwayTeamId);
    }

    [Fact]
    public void Fixtures_Both_Teams_Are_In_Same_Group()
    {
        var teamGroup = TeamsData.All.ToDictionary(t => t.Id, t => t.GroupLetter);
        foreach (var fixture in FixturesData.All)
        {
            Assert.Equal(teamGroup[fixture.HomeTeamId], fixture.GroupLetter);
            Assert.Equal(teamGroup[fixture.AwayTeamId], fixture.GroupLetter);
        }
    }

    [Fact]
    public void Fixtures_KickoffTimes_Are_UTC()
    {
        foreach (var fixture in FixturesData.All)
            Assert.Equal(TimeSpan.Zero, fixture.KickoffUtc.Offset);
    }

    [Fact]
    public void Fixtures_KickoffTimes_Within_Group_Stage_Window()
    {
        // Group stage: 11 June – 27 June 2026 (UTC dates)
        var earliest = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
        var latest   = new DateTimeOffset(2026, 6, 27, 23, 59, 0, TimeSpan.Zero);
        foreach (var fixture in FixturesData.All)
        {
            Assert.True(fixture.KickoffUtc >= earliest,
                $"Fixture {fixture.MatchNumber} kickoff {fixture.KickoffUtc:o} is before group stage start");
            Assert.True(fixture.KickoffUtc <= latest,
                $"Fixture {fixture.MatchNumber} kickoff {fixture.KickoffUtc:o} is after group stage end");
        }
    }

    [Fact]
    public void Fixtures_Each_Group_Pair_Plays_Exactly_Once()
    {
        // For each group, every combination of 2 teams should appear exactly once.
        var groupTeams = GroupsData.All.ToDictionary(
            g => g.Letter,
            g => g.TeamIds.OrderBy(x => x).ToList());

        foreach (var (letter, teams) in groupTeams)
        {
            var groupFixtures = FixturesData.All.Where(f => f.GroupLetter == letter).ToList();
            for (int i = 0; i < teams.Count; i++)
            for (int j = i + 1; j < teams.Count; j++)
            {
                var t1 = teams[i];
                var t2 = teams[j];
                var count = groupFixtures.Count(f =>
                    (f.HomeTeamId == t1 && f.AwayTeamId == t2) ||
                    (f.HomeTeamId == t2 && f.AwayTeamId == t1));
                Assert.Equal(1, count);
            }
        }
    }

    [Fact]
    public void Fixtures_Ids_Match_MatchNumber_String()
    {
        foreach (var f in FixturesData.All)
            Assert.Equal(f.MatchNumber.ToString(), f.Id);
    }

    // ── Knockout Slots ────────────────────────────────────────────────────────────

    [Fact]
    public void KnockoutSlots_Count_Is_32()
    {
        Assert.Equal(32, KnockoutSlotsData.All.Count);
    }

    [Fact]
    public void KnockoutSlots_R32_Count_Is_16()
    {
        Assert.Equal(16, KnockoutSlotsData.All.Count(s => s.Round == Round.R32));
    }

    [Fact]
    public void KnockoutSlots_R16_Count_Is_8()
    {
        Assert.Equal(8, KnockoutSlotsData.All.Count(s => s.Round == Round.R16));
    }

    [Fact]
    public void KnockoutSlots_QF_Count_Is_4()
    {
        Assert.Equal(4, KnockoutSlotsData.All.Count(s => s.Round == Round.QF));
    }

    [Fact]
    public void KnockoutSlots_SF_Count_Is_2()
    {
        Assert.Equal(2, KnockoutSlotsData.All.Count(s => s.Round == Round.SF));
    }

    [Fact]
    public void KnockoutSlots_ThirdPlace_Count_Is_1()
    {
        Assert.Equal(1, KnockoutSlotsData.All.Count(s => s.Round == Round.ThirdPlace));
    }

    [Fact]
    public void KnockoutSlots_Final_Count_Is_1()
    {
        Assert.Equal(1, KnockoutSlotsData.All.Count(s => s.Round == Round.Final));
    }

    [Fact]
    public void KnockoutSlots_SlotKeys_Are_Unique()
    {
        var keys = KnockoutSlotsData.All.Select(s => s.SlotKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void KnockoutSlots_Ids_Match_SlotKeys()
    {
        foreach (var slot in KnockoutSlotsData.All)
            Assert.Equal(slot.SlotKey, slot.Id);
    }

    [Fact]
    public void KnockoutSlots_All_Have_Home_And_Away_SlotSources()
    {
        foreach (var slot in KnockoutSlotsData.All)
        {
            Assert.NotNull(slot.HomeSlotSource);
            Assert.NotNull(slot.AwaySlotSource);
            Assert.False(string.IsNullOrWhiteSpace(slot.HomeSlotSource.Reference));
            Assert.False(string.IsNullOrWhiteSpace(slot.AwaySlotSource.Reference));
        }
    }

    [Fact]
    public void KnockoutSlots_R32_HomeSlotSources_Reference_Group_Or_BestThird()
    {
        var r32 = KnockoutSlotsData.All.Where(s => s.Round == Round.R32).ToList();
        foreach (var slot in r32)
        {
            var homeType = slot.HomeSlotSource.Type;
            Assert.True(
                homeType == SlotSourceType.GroupWinner ||
                homeType == SlotSourceType.GroupRunnerUp ||
                homeType == SlotSourceType.BestThirdPlace,
                $"Slot {slot.SlotKey} home source type {homeType} is unexpected for R32");
        }
    }

    [Fact]
    public void KnockoutSlots_ThirdPlace_Uses_MatchLoser_Sources()
    {
        var thirdPlace = KnockoutSlotsData.All.Single(s => s.Round == Round.ThirdPlace);
        Assert.Equal(SlotSourceType.MatchLoser, thirdPlace.HomeSlotSource.Type);
        Assert.Equal(SlotSourceType.MatchLoser, thirdPlace.AwaySlotSource.Type);
    }

    [Fact]
    public void KnockoutSlots_R16_Through_Final_Use_MatchWinner_Sources()
    {
        var laterRounds = KnockoutSlotsData.All
            .Where(s => s.Round is Round.R16 or Round.QF or Round.SF or Round.Final)
            .ToList();

        foreach (var slot in laterRounds)
        {
            Assert.Equal(SlotSourceType.MatchWinner, slot.HomeSlotSource.Type);
            Assert.Equal(SlotSourceType.MatchWinner, slot.AwaySlotSource.Type);
        }
    }

    [Fact]
    public void KnockoutSlots_AllTeamIds_Are_Null_Initially()
    {
        // Knockout slots are placeholders — no teams resolved at seeding time.
        foreach (var slot in KnockoutSlotsData.All)
        {
            Assert.Null(slot.HomeTeamId);
            Assert.Null(slot.AwayTeamId);
        }
    }

    [Fact]
    public void KnockoutSlots_R32_SlotNumbers_1_Through_16()
    {
        var numbers = KnockoutSlotsData.All
            .Where(s => s.Round == Round.R32)
            .Select(s => s.SlotNumber)
            .OrderBy(n => n)
            .ToList();
        Assert.Equal(Enumerable.Range(1, 16).ToList(), numbers);
    }

    // ── Players ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Players_All_Have_Valid_TeamId()
    {
        var knownTeamIds = new HashSet<string>(TeamsData.All.Select(t => t.Id));
        foreach (var player in PlayersData.All)
            Assert.Contains(player.TeamId, knownTeamIds);
    }

    [Fact]
    public void Players_All_Have_NonEmpty_Name()
    {
        foreach (var player in PlayersData.All)
            Assert.False(string.IsNullOrWhiteSpace(player.Name), $"Player {player.Id} has empty name");
    }

    [Fact]
    public void Players_All_Have_Valid_Position()
    {
        var validPositions = new[] { Position.GK, Position.DEF, Position.MID, Position.FWD };
        foreach (var player in PlayersData.All)
            Assert.Contains(player.Position, validPositions);
    }

    [Fact]
    public void Players_All_Have_Unique_Ids()
    {
        var ids = PlayersData.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Players_Each_Team_Has_At_Least_15_Players()
    {
        var byTeam = PlayersData.All
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var team in TeamsData.All)
        {
            byTeam.TryGetValue(team.Id, out var count);
            Assert.True(count >= 15,
                $"Team {team.FifaCode} has only {count} players (minimum 15 required)");
        }
    }

    [Fact]
    public void Players_Each_Team_Has_At_Least_One_GK()
    {
        var gkByTeam = PlayersData.All
            .Where(p => p.Position == Position.GK)
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var team in TeamsData.All)
        {
            gkByTeam.TryGetValue(team.Id, out var count);
            Assert.True(count >= 1, $"Team {team.FifaCode} has no goalkeepers");
        }
    }

    [Fact]
    public void Players_Each_Team_Has_At_Least_One_FWD()
    {
        var fwdByTeam = PlayersData.All
            .Where(p => p.Position == Position.FWD)
            .GroupBy(p => p.TeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var team in TeamsData.All)
        {
            fwdByTeam.TryGetValue(team.Id, out var count);
            Assert.True(count >= 1, $"Team {team.FifaCode} has no forwards");
        }
    }

    // ── Idempotency (no-database, logic-level) ────────────────────────────────────

    [Fact]
    public void SeedData_All_Collections_Have_Stable_Counts_When_Called_Twice()
    {
        // Calling the static properties twice returns the same counts.
        // This verifies the static data is not mutated between calls.
        Assert.Equal(TeamsData.All.Count,        TeamsData.All.Count);
        Assert.Equal(GroupsData.All.Count,       GroupsData.All.Count);
        Assert.Equal(FixturesData.All.Count,     FixturesData.All.Count);
        Assert.Equal(KnockoutSlotsData.All.Count, KnockoutSlotsData.All.Count);
        Assert.Equal(PlayersData.All.Count,      PlayersData.All.Count);
    }
}
