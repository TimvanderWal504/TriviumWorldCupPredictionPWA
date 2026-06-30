using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Scoring;

namespace TriviumWorldCup.Api.Tests.Scoring;

/// <summary>
/// Verifies that KnockoutStreakCalculator derives the correct streakBefore from the
/// bracket feeder chain — not from a global per-user counter.
///
/// These tests cover the scenarios from the streak-multiplier bug fix. All run in
/// memory; no database is required.
/// </summary>
public class KnockoutStreakCalculatorTests
{
    private const string User = "user1";

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static KnockoutSlot Slot(
        string slotKey,
        Round round,
        int slotNumber,
        string home,
        string away,
        string winner,
        string homeSourceRef = "",
        SlotSourceType homeSourceType = SlotSourceType.GroupWinner,
        string awaySourceRef = "",
        SlotSourceType awaySourceType = SlotSourceType.GroupRunnerUp) => new()
    {
        Id = slotKey,
        SlotKey = slotKey,
        Round = round,
        SlotNumber = slotNumber,
        HomeTeamId = home,
        AwayTeamId = away,
        WinnerTeamId = winner,
        Status = MatchStatus.Completed,
        HomeSlotSource = new SlotSource { Type = homeSourceType, Reference = homeSourceRef },
        AwaySlotSource = new SlotSource { Type = awaySourceType, Reference = awaySourceRef },
    };

    private static KnockoutPrediction Pred(string slotKey, string predictedWinner) => new()
    {
        Id = $"{User}_{slotKey}",
        UserId = User,
        SlotKey = slotKey,
        PredictedWinnerTeamId = predictedWinner,
    };

    private static int StreakBefore(
        string slotKey,
        IEnumerable<KnockoutSlot> slots,
        IEnumerable<KnockoutPrediction> preds)
    {
        var slotByKey = slots.ToDictionary(s => s.SlotKey);
        var predsByUserAndSlot = preds
            .GroupBy(p => p.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, KnockoutPrediction>)g.ToDictionary(p => p.SlotKey));

        return KnockoutStreakCalculator.StreakBefore(User, slotKey, slotByKey, predsByUserAndSlot);
    }

    // ── Test 1: Two different R32 teams, both correct — each streakBefore = 0 ───
    // Reported bug: the second R32 pick returned streakBefore=1, yielding 5×2 instead of 5×1.

    [Fact]
    public void TwoR32Teams_BothCorrect_EachStreakBeforeIs0()
    {
        var slots = new[]
        {
            Slot("R32-1", Round.R32, 1, "BRA", "GER", winner: "BRA"),
            Slot("R32-2", Round.R32, 2, "ARG", "FRA", winner: "ARG"),
        };
        var preds = new[]
        {
            Pred("R32-1", "BRA"),
            Pred("R32-2", "ARG"),
        };

        Assert.Equal(0, StreakBefore("R32-1", slots, preds));
        Assert.Equal(0, StreakBefore("R32-2", slots, preds));
    }

    // ── Test 2: Same team R32→R16→QF all correct — streakBefore grows per round ──

    [Fact]
    public void SameTeam_R32_R16_QF_Correct_StreakGrows()
    {
        var slots = new[]
        {
            Slot("R32-1", Round.R32, 1, "BRA", "GER", winner: "BRA"),
            Slot("R16-1", Round.R16, 1, "BRA", "ESP", winner: "BRA",
                homeSourceRef: "R32-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R32-2", awaySourceType: SlotSourceType.MatchWinner),
            Slot("QF-1",  Round.QF, 1, "BRA", "ARG", winner: "BRA",
                homeSourceRef: "R16-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R16-2", awaySourceType: SlotSourceType.MatchWinner),
        };
        var preds = new[]
        {
            Pred("R32-1", "BRA"),
            Pred("R16-1", "BRA"),
            Pred("QF-1",  "BRA"),
        };

        Assert.Equal(0, StreakBefore("R32-1", slots, preds)); // first pick, no feeder
        Assert.Equal(1, StreakBefore("R16-1", slots, preds)); // BRA won R32-1
        Assert.Equal(2, StreakBefore("QF-1",  slots, preds)); // BRA won R16-1 after R32-1
    }

    // ── Test 3: Chain broken — R32 correct, R16 wrong, QF correct again → QF restarts ──

    [Fact]
    public void ChainBroken_R16Wrong_QFRestartsAt0()
    {
        var slots = new[]
        {
            Slot("R32-1", Round.R32, 1, "BRA", "GER", winner: "BRA"),
            Slot("R16-1", Round.R16, 1, "BRA", "ESP", winner: "BRA",
                homeSourceRef: "R32-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R32-2", awaySourceType: SlotSourceType.MatchWinner),
            Slot("QF-1",  Round.QF, 1, "BRA", "ARG", winner: "BRA",
                homeSourceRef: "R16-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R16-2", awaySourceType: SlotSourceType.MatchWinner),
        };
        var preds = new[]
        {
            Pred("R32-1", "BRA"),
            Pred("R16-1", "GER"), // wrong — predicted the loser
            Pred("QF-1",  "BRA"),
        };

        Assert.Equal(0, StreakBefore("R32-1", slots, preds));
        Assert.Equal(0, StreakBefore("R16-1", slots, preds)); // wrong pick → 0
        Assert.Equal(0, StreakBefore("QF-1",  slots, preds)); // chain broken at R16 → fresh start
    }

    // ── Test 4: Round skipped (no R16 prediction) — QF correct = streakBefore 0 ──

    [Fact]
    public void SkippedRound_QFStreakBeforeIs0()
    {
        var slots = new[]
        {
            Slot("R32-1", Round.R32, 1, "BRA", "GER", winner: "BRA"),
            Slot("R16-1", Round.R16, 1, "BRA", "ESP", winner: "BRA",
                homeSourceRef: "R32-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R32-2", awaySourceType: SlotSourceType.MatchWinner),
            Slot("QF-1",  Round.QF, 1, "BRA", "ARG", winner: "BRA",
                homeSourceRef: "R16-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R16-2", awaySourceType: SlotSourceType.MatchWinner),
        };
        var preds = new[]
        {
            Pred("R32-1", "BRA"),
            // no R16-1 prediction
            Pred("QF-1",  "BRA"),
        };

        Assert.Equal(0, StreakBefore("QF-1", slots, preds)); // no R16 pick → chain broken
    }

    // ── Test 5: Third-place play-off — feeder is MatchLoser → streakBefore always 0 ──

    [Fact]
    public void ThirdPlace_MatchLoserFeeder_StreakBeforeIs0()
    {
        var slots = new[]
        {
            Slot("SF-1", Round.SF, 1, "BRA", "ARG", winner: "BRA",
                homeSourceRef: "QF-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "QF-2", awaySourceType: SlotSourceType.MatchWinner),
            Slot("3RD", Round.ThirdPlace, 1, "ARG", "ESP", winner: "ARG",
                homeSourceRef: "SF-1", homeSourceType: SlotSourceType.MatchLoser,  // loser bracket
                awaySourceRef: "SF-2", awaySourceType: SlotSourceType.MatchLoser),
        };
        var preds = new[]
        {
            Pred("SF-1", "BRA"), // BRA won SF — user was right
            Pred("3RD",  "ARG"), // ARG won 3rd-place — user was right
        };

        // The 3RD feeder is MatchLoser, so no winning chain can be extended.
        Assert.Equal(0, StreakBefore("3RD", slots, preds));
    }

    // ── Test 6: Score component stays separate — full points check via scorer ──
    // Exact 90-min score + correct winner in R16 with streakBefore=1 → 10 + 5×2 = 20.

    [Fact]
    public void ExactScore_CorrectWinner_R16_Streak1_Returns20()
    {
        var slots = new[]
        {
            Slot("R32-1", Round.R32, 1, "ESP", "GER", winner: "ESP"),
            Slot("R16-1", Round.R16, 1, "ESP", "BRA", winner: "ESP",
                homeSourceRef: "R32-1", homeSourceType: SlotSourceType.MatchWinner,
                awaySourceRef: "R32-2", awaySourceType: SlotSourceType.MatchWinner),
        };
        var preds = new[]
        {
            Pred("R32-1", "ESP"),
            Pred("R16-1", "ESP"),
        };

        var streakBefore = StreakBefore("R16-1", slots, preds);
        Assert.Equal(1, streakBefore);

        // Plug into scorer: exact 90-min score (2-1) + winner correct with streak 1
        var pts = KnockoutMatchScorer.Compute("ESP", 2, 1, "ESP", 2, 1, streakBefore);
        Assert.Equal(20, pts); // 10 (exact) + 5×2 = 20
    }
}
