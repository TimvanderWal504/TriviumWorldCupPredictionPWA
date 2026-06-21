using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// 32-slot knockout bracket for FIFA World Cup 2026.
///
/// Structure:
///   Round of 32 (R32): 16 matches — slots R32-1 through R32-16
///   Round of 16 (R16):  8 matches — slots R16-1 through R16-8
///   Quarter-finals (QF): 4 matches — slots QF-1 through QF-4
///   Semi-finals (SF):    2 matches — slots SF-1, SF-2
///   3rd-place play-off:  1 match  — slot 3RD
///   Final:               1 match  — slot FIN
///   Total: 16+8+4+2+1+1 = 32 slots
///
/// Qualification to R32:
///   - 12 group winners (W-A through W-L)
///   - 12 group runners-up (RU-A through RU-L)
///   - 8 best third-placed teams (3P)
///   Total: 32 teams
///
/// Verified against: FIFA.com official knockout bracket, cross-checked against
/// Sky Sports, BBC/NBC Sports, CBS Sports, Bleacher Report, and ESPN match
/// schedules (20 June 2026). Kickoff times converted from BST (UTC+1) to UTC.
///
/// NOTE — BestThirdPlace resolver: the Reference strings below use 5-group
/// eligibility sets (e.g. "A/B/C/D/F") as published by FIFA. The current
/// resolver iterates Reference letters and returns the first qualifying team it
/// finds in bestThirdByGroup. This worked under the old 3-group bijection but
/// may assign incorrectly when multiple eligible groups qualify. A full
/// FIFA-2026 matrix-based allocation (C(12,8) = 495 rows) is needed to handle
/// the general case correctly — tracked as a follow-up before the group stage
/// completes (first R32 match: 28 June 2026).
/// </summary>
public static class KnockoutSlotsData
{
    public static IReadOnlyList<KnockoutSlot> All => _all;

    private static SlotSource Src(SlotSourceType type, string reference)
        => new() { Type = type, Reference = reference };

    private static readonly List<KnockoutSlot> _all =
    [
        // ═══════════════════════════════════════════════════════════════════
        // ROUND OF 32  (16 matches, M73–M88)
        // Slots numbered in FIFA chronological kickoff order.
        // Wiring and schedule verified against official FIFA 2026 bracket.
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "R32-1", SlotKey = "R32-1", Round = Round.R32, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp,  "A"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "B"),
            KickoffUtc = new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero),
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "R32-2", SlotKey = "R32-2", Round = Round.R32, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "C"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "F"),
            KickoffUtc = new DateTimeOffset(2026, 6, 29, 17, 0, 0, TimeSpan.Zero),
            Venue = "NRG Stadium", City = "Houston"
        },
        new()
        {
            Id = "R32-3", SlotKey = "R32-3", Round = Round.R32, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "E"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "A/B/C/D/F"),
            KickoffUtc = new DateTimeOffset(2026, 6, 29, 20, 30, 0, TimeSpan.Zero),
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "R32-4", SlotKey = "R32-4", Round = Round.R32, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "F"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "C"),
            KickoffUtc = new DateTimeOffset(2026, 6, 30, 1, 0, 0, TimeSpan.Zero),
            Venue = "Estadio BBVA", City = "Monterrey"
        },
        new()
        {
            Id = "R32-5", SlotKey = "R32-5", Round = Round.R32, SlotNumber = 5,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp,  "E"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "I"),
            KickoffUtc = new DateTimeOffset(2026, 6, 30, 17, 0, 0, TimeSpan.Zero),
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R32-6", SlotKey = "R32-6", Round = Round.R32, SlotNumber = 6,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "I"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "C/D/F/G/H"),
            KickoffUtc = new DateTimeOffset(2026, 6, 30, 21, 0, 0, TimeSpan.Zero),
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "R32-7", SlotKey = "R32-7", Round = Round.R32, SlotNumber = 7,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "A"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "C/E/F/H/I"),
            KickoffUtc = new DateTimeOffset(2026, 7, 1, 1, 0, 0, TimeSpan.Zero),
            Venue = "Estadio Azteca", City = "Mexico City"
        },
        new()
        {
            Id = "R32-8", SlotKey = "R32-8", Round = Round.R32, SlotNumber = 8,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "L"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "E/H/I/J/K"),
            KickoffUtc = new DateTimeOffset(2026, 7, 1, 16, 0, 0, TimeSpan.Zero),
            Venue = "Mercedes-Benz Stadium", City = "Atlanta"
        },
        new()
        {
            Id = "R32-9", SlotKey = "R32-9", Round = Round.R32, SlotNumber = 9,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "G"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "A/E/H/I/J"),
            KickoffUtc = new DateTimeOffset(2026, 7, 1, 20, 0, 0, TimeSpan.Zero),
            Venue = "Lumen Field", City = "Seattle"
        },
        new()
        {
            Id = "R32-10", SlotKey = "R32-10", Round = Round.R32, SlotNumber = 10,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "D"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "B/E/F/I/J"),
            KickoffUtc = new DateTimeOffset(2026, 7, 2, 0, 0, 0, TimeSpan.Zero),
            Venue = "Levi's Stadium", City = "San Francisco"
        },
        new()
        {
            Id = "R32-11", SlotKey = "R32-11", Round = Round.R32, SlotNumber = 11,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "H"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "J"),
            KickoffUtc = new DateTimeOffset(2026, 7, 2, 19, 0, 0, TimeSpan.Zero),
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "R32-12", SlotKey = "R32-12", Round = Round.R32, SlotNumber = 12,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp,  "K"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "L"),
            KickoffUtc = new DateTimeOffset(2026, 7, 2, 23, 0, 0, TimeSpan.Zero),
            Venue = "BMO Field", City = "Toronto"
        },
        new()
        {
            Id = "R32-13", SlotKey = "R32-13", Round = Round.R32, SlotNumber = 13,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "B"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "E/F/G/I/J"),
            KickoffUtc = new DateTimeOffset(2026, 7, 3, 3, 0, 0, TimeSpan.Zero),
            Venue = "BC Place", City = "Vancouver"
        },
        new()
        {
            Id = "R32-14", SlotKey = "R32-14", Round = Round.R32, SlotNumber = 14,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp,  "D"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "G"),
            KickoffUtc = new DateTimeOffset(2026, 7, 3, 18, 0, 0, TimeSpan.Zero),
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R32-15", SlotKey = "R32-15", Round = Round.R32, SlotNumber = 15,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "J"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "H"),
            KickoffUtc = new DateTimeOffset(2026, 7, 3, 22, 0, 0, TimeSpan.Zero),
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "R32-16", SlotKey = "R32-16", Round = Round.R32, SlotNumber = 16,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "K"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "D/E/I/J/L"),
            KickoffUtc = new DateTimeOffset(2026, 7, 4, 1, 30, 0, TimeSpan.Zero),
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // ═══════════════════════════════════════════════════════════════════
        // ROUND OF 16  (8 matches, M89–M96)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "R16-1", SlotKey = "R16-1", Round = Round.R16, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-6"),
            KickoffUtc = new DateTimeOffset(2026, 7, 4, 21, 0, 0, TimeSpan.Zero),
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },
        new()
        {
            Id = "R16-2", SlotKey = "R16-2", Round = Round.R16, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 4, 17, 0, 0, TimeSpan.Zero),
            Venue = "NRG Stadium", City = "Houston"
        },
        new()
        {
            Id = "R16-3", SlotKey = "R16-3", Round = Round.R16, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-2"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-5"),
            KickoffUtc = new DateTimeOffset(2026, 7, 5, 20, 0, 0, TimeSpan.Zero),
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "R16-4", SlotKey = "R16-4", Round = Round.R16, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-7"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-8"),
            KickoffUtc = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            Venue = "Estadio Azteca", City = "Mexico City"
        },
        new()
        {
            Id = "R16-5", SlotKey = "R16-5", Round = Round.R16, SlotNumber = 5,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-9"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-10"),
            KickoffUtc = new DateTimeOffset(2026, 7, 7, 0, 0, 0, TimeSpan.Zero),
            Venue = "Lumen Field", City = "Seattle"
        },
        new()
        {
            Id = "R16-6", SlotKey = "R16-6", Round = Round.R16, SlotNumber = 6,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-11"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-12"),
            KickoffUtc = new DateTimeOffset(2026, 7, 6, 19, 0, 0, TimeSpan.Zero),
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R16-7", SlotKey = "R16-7", Round = Round.R16, SlotNumber = 7,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-15"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-14"),
            KickoffUtc = new DateTimeOffset(2026, 7, 7, 16, 0, 0, TimeSpan.Zero),
            Venue = "Mercedes-Benz Stadium", City = "Atlanta"
        },
        new()
        {
            Id = "R16-8", SlotKey = "R16-8", Round = Round.R16, SlotNumber = 8,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-13"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-16"),
            KickoffUtc = new DateTimeOffset(2026, 7, 7, 20, 0, 0, TimeSpan.Zero),
            Venue = "BC Place", City = "Vancouver"
        },

        // ═══════════════════════════════════════════════════════════════════
        // QUARTER-FINALS  (4 matches, M97–M100)
        // QF-2 and QF-3 sources were swapped in the original seed; corrected.
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "QF-1", SlotKey = "QF-1", Round = Round.QF, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 9, 20, 0, 0, TimeSpan.Zero),
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "QF-2", SlotKey = "QF-2", Round = Round.QF, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-6"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-5"),
            KickoffUtc = new DateTimeOffset(2026, 7, 10, 19, 0, 0, TimeSpan.Zero),
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "QF-3", SlotKey = "QF-3", Round = Round.QF, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 11, 21, 0, 0, TimeSpan.Zero),
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "QF-4", SlotKey = "QF-4", Round = Round.QF, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-7"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-8"),
            KickoffUtc = new DateTimeOffset(2026, 7, 12, 1, 0, 0, TimeSpan.Zero),
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // ═══════════════════════════════════════════════════════════════════
        // SEMI-FINALS  (2 matches, M101–M102)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "SF-1", SlotKey = "SF-1", Round = Round.SF, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "QF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "QF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 14, 19, 0, 0, TimeSpan.Zero),
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "SF-2", SlotKey = "SF-2", Round = Round.SF, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "QF-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "QF-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 15, 19, 0, 0, TimeSpan.Zero),
            Venue = "Mercedes-Benz Stadium", City = "Atlanta"
        },

        // ═══════════════════════════════════════════════════════════════════
        // THIRD-PLACE PLAY-OFF  (1 match, M103)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "3RD", SlotKey = "3RD", Round = Round.ThirdPlace, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchLoser, "SF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchLoser, "SF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 18, 21, 0, 0, TimeSpan.Zero),
            Venue = "Hard Rock Stadium", City = "Miami"
        },

        // ═══════════════════════════════════════════════════════════════════
        // FINAL  (1 match, M104)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "FIN", SlotKey = "FIN", Round = Round.Final, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "SF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "SF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 19, 19, 0, 0, TimeSpan.Zero),
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
    ];
}
