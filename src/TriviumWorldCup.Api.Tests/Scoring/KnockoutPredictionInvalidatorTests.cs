using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// TWC-63: after a bracket rewiring (TWC-62 migration) or an admin team override
/// (POST /admin/knockout/{slotKey}/teams), existing KnockoutPrediction documents whose
/// PredictedWinnerTeamId is no longer one of the slot's participants must be treated as stale.
/// Pure function — no database required.
/// </summary>
public class KnockoutPredictionInvalidatorTests
{
    private static KnockoutSlot Slot(string slotKey, string? home, string? away) => new()
    {
        Id         = slotKey,
        SlotKey    = slotKey,
        Round      = Round.R32,
        SlotNumber = 1,
        HomeTeamId = home,
        AwayTeamId = away,
        HomeSlotSource = new SlotSource { Type = SlotSourceType.GroupWinner, Reference = "A" },
        AwaySlotSource = new SlotSource { Type = SlotSourceType.GroupRunnerUp, Reference = "B" },
    };

    private static KnockoutPrediction Pred(string slotKey, string userId, string predictedWinner) => new()
    {
        Id                    = $"{userId}_{slotKey}",
        UserId                = userId,
        SlotKey               = slotKey,
        PredictedWinnerTeamId = predictedWinner,
    };

    [Fact]
    public void PredictedWinner_StillAParticipant_NotStale()
    {
        var slot = Slot("R32-3", home: "BIH", away: "USA");
        var preds = new[] { Pred("R32-3", "user1", "BIH") };

        var stale = KnockoutPredictionInvalidator.FindStale(slot, preds);

        Assert.Empty(stale);
    }

    [Fact]
    public void AdminOverrideRemovesPredictedTeam_PredictionBecomesStale()
    {
        // Original matchup: BIH vs AUS. User predicted BIH to win.
        // Admin corrects the override: AUS is replaced by USA (BIH stays home).
        var correctedSlot = Slot("R32-3", home: "BIH", away: "USA");
        var preds = new[] { Pred("R32-3", "user1", "AUS") }; // predicted the team that got removed

        var stale = KnockoutPredictionInvalidator.FindStale(correctedSlot, preds);

        Assert.Single(stale);
        Assert.Equal("user1", stale[0].UserId);
    }

    [Fact]
    public void PredictionForDifferentSlot_Unaffected()
    {
        var slot = Slot("R32-3", home: "BIH", away: "USA");
        var preds = new[]
        {
            Pred("R32-3", "user1", "AUS"),  // stale
            Pred("R32-4", "user1", "GER"),  // different slot entirely — caller responsible for filtering by slot, but the invalidator itself only checks participancy on the given slot
        };

        var stale = KnockoutPredictionInvalidator.FindStale(slot, preds);

        // Both are checked against `slot` here since the invalidator doesn't filter by SlotKey
        // itself (that's the caller's responsibility) — GER is also not a BIH/USA participant.
        Assert.Equal(2, stale.Count);
    }

    [Fact]
    public void CaseInsensitiveMatch_NotStale()
    {
        var slot = Slot("R32-3", home: "BIH", away: "USA");
        var preds = new[] { Pred("R32-3", "user1", "bih") };

        var stale = KnockoutPredictionInvalidator.FindStale(slot, preds);

        Assert.Empty(stale);
    }

    [Fact]
    public void BothTeamsNulledByWiringChange_AllPredictionsStale()
    {
        var slot = Slot("R32-3", home: null, away: null);
        var preds = new[]
        {
            Pred("R32-3", "user1", "BIH"),
            Pred("R32-3", "user2", "AUS"),
        };

        var stale = KnockoutPredictionInvalidator.FindStale(slot, preds);

        Assert.Equal(2, stale.Count);
    }

    [Fact]
    public void NoPredictions_ReturnsEmpty()
    {
        var slot = Slot("R32-3", home: "BIH", away: "USA");
        var stale = KnockoutPredictionInvalidator.FindStale(slot, Array.Empty<KnockoutPrediction>());
        Assert.Empty(stale);
    }
}
