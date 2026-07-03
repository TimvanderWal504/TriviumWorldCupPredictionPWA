using TriviumWorldCup.Api.Data;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tests.Data;

/// <summary>
/// TWC-62: TournamentSeed.ApplySlotMigration (extracted from MigrateKnockoutSlotsAsync) clears
/// HomeTeamId/AwayTeamId when a slot's wiring changes — but must also clear the corresponding
/// *Overridden flags, since KnockoutBracketResolver skips writes on overridden slots. Leaving an
/// override flag set after a wiring change would leave the slot permanently teamless.
/// Pure function — no database required.
/// </summary>
public class TournamentSeedMigrationTests
{
    private static KnockoutSlot MakeSlot(
        string slotKey,
        SlotSourceType homeType, string homeRef,
        SlotSourceType awayType, string awayRef,
        string? homeTeamId = "BRA",
        string? awayTeamId = "ARG",
        bool homeOverridden = false,
        bool awayOverridden = false) => new()
        {
            Id                 = slotKey,
            SlotKey            = slotKey,
            Round              = Round.R32,
            SlotNumber         = 1,
            HomeSlotSource     = new SlotSource { Type = homeType, Reference = homeRef },
            AwaySlotSource     = new SlotSource { Type = awayType, Reference = awayRef },
            HomeTeamId         = homeTeamId,
            AwayTeamId         = awayTeamId,
            HomeTeamOverridden = homeOverridden,
            AwayTeamOverridden = awayOverridden,
            KickoffUtc         = new DateTimeOffset(2026, 6, 15, 18, 0, 0, TimeSpan.Zero),
            Venue              = "Old Venue",
            City               = "Old City",
        };

    // ── Wiring change clears team IDs AND overridden flags ────────────────────

    [Fact]
    public void WiringChanged_OverriddenSlot_ClearsTeamIdsAndOverrideFlags()
    {
        var existing = MakeSlot(
            "R32-3",
            SlotSourceType.GroupWinner, "B",
            SlotSourceType.GroupRunnerUp, "A",
            homeTeamId: "BIH", awayTeamId: "AUS",
            homeOverridden: true, awayOverridden: true);

        // Corrected wiring: away source reference changes (e.g. bracket correction).
        var template = MakeSlot(
            "R32-3",
            SlotSourceType.GroupWinner, "B",
            SlotSourceType.GroupRunnerUp, "K", // corrected reference
            homeTeamId: null, awayTeamId: null);

        var changed = TournamentSeed.ApplySlotMigration(existing, template);

        Assert.True(changed);
        Assert.Null(existing.HomeTeamId);
        Assert.Null(existing.AwayTeamId);
        Assert.False(existing.HomeTeamOverridden);
        Assert.False(existing.AwayTeamOverridden);
        Assert.Equal(SlotSourceType.GroupRunnerUp, existing.AwaySlotSource.Type);
        Assert.Equal("K", existing.AwaySlotSource.Reference);
    }

    [Fact]
    public void WiringChanged_HomeOverriddenOnly_ClearsOnlyRelevantButBothIdsCleared()
    {
        // Even when only one side was overridden, both team IDs are cleared on wiring change
        // (matches existing pre-fix behavior) — but the override flags must both be cleared too,
        // since either side's source may have shifted.
        var existing = MakeSlot(
            "R32-5",
            SlotSourceType.GroupWinner, "C",
            SlotSourceType.GroupRunnerUp, "D",
            homeTeamId: "FRA", awayTeamId: "DEN",
            homeOverridden: true, awayOverridden: false);

        var template = MakeSlot(
            "R32-5",
            SlotSourceType.GroupWinner, "C",
            SlotSourceType.GroupRunnerUp, "E", // wiring changed
            homeTeamId: null, awayTeamId: null);

        var changed = TournamentSeed.ApplySlotMigration(existing, template);

        Assert.True(changed);
        Assert.Null(existing.HomeTeamId);
        Assert.Null(existing.AwayTeamId);
        Assert.False(existing.HomeTeamOverridden);
        Assert.False(existing.AwayTeamOverridden);
    }

    // ── No wiring change → team IDs and override flags untouched ──────────────

    [Fact]
    public void NoWiringChange_OverriddenSlot_TeamIdsAndOverrideFlagsUntouched()
    {
        var existing = MakeSlot(
            "R32-7",
            SlotSourceType.GroupWinner, "E",
            SlotSourceType.GroupRunnerUp, "F",
            homeTeamId: "ESP", awayTeamId: "JPN",
            homeOverridden: true, awayOverridden: true);

        // Only metadata (venue) changes — wiring identical.
        var template = MakeSlot(
            "R32-7",
            SlotSourceType.GroupWinner, "E",
            SlotSourceType.GroupRunnerUp, "F",
            homeTeamId: null, awayTeamId: null);
        template.Venue = "New Venue";

        var changed = TournamentSeed.ApplySlotMigration(existing, template);

        Assert.True(changed); // metaChanged still triggers an update
        Assert.Equal("ESP", existing.HomeTeamId);
        Assert.Equal("JPN", existing.AwayTeamId);
        Assert.True(existing.HomeTeamOverridden);
        Assert.True(existing.AwayTeamOverridden);
        Assert.Equal("New Venue", existing.Venue);
    }

    [Fact]
    public void NoChangeAtAll_ReturnsFalse_NothingMutated()
    {
        var existing = MakeSlot(
            "R32-9",
            SlotSourceType.GroupWinner, "G",
            SlotSourceType.GroupRunnerUp, "H",
            homeTeamId: "POR", awayTeamId: "URU");

        var template = MakeSlot(
            "R32-9",
            SlotSourceType.GroupWinner, "G",
            SlotSourceType.GroupRunnerUp, "H",
            homeTeamId: null, awayTeamId: null);

        var changed = TournamentSeed.ApplySlotMigration(existing, template);

        Assert.False(changed);
        Assert.Equal("POR", existing.HomeTeamId);
        Assert.Equal("URU", existing.AwayTeamId);
    }
}
