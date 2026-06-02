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
/// FIFA bracket structure for R32:
///   The exact R32 seeding matrix depends on the official FIFA draw for the
///   knockout bracket (drawn after group stage). At seeding time only the
///   pot assignments are known. The SlotSource references below use the
///   FIFA-published bracket template (Dec 2024).
///
/// IMPORTANT: The exact cross-bracket wiring (which group positions fill which
/// R32 slot) depends on the official FIFA bracket template. Entries are marked
/// // TODO: verify bracket wiring where the exact wiring is uncertain.
/// </summary>
public static class KnockoutSlotsData
{
    public static IReadOnlyList<KnockoutSlot> All => _all;

    private static SlotSource Src(SlotSourceType type, string reference)
        => new() { Type = type, Reference = reference };

    private static readonly List<KnockoutSlot> _all =
    [
        // ═══════════════════════════════════════════════════════════════════
        // ROUND OF 32  (16 matches)
        // Based on the FIFA 2026 bracket template:
        // Group winners are seeded on one side, runners-up on the other,
        // and 8 best third-placed teams fill the remaining positions.
        // The specific matchup per FIFA bracket is:
        //   R32-1:  W-A vs best 3rd of B/C/D/E/F
        //   R32-2:  W-B vs best 3rd of A/C/D/E/F
        //   ...
        // TODO: verify exact bracket wiring from FIFA official draw template
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "R32-1", SlotKey = "R32-1", Round = Round.R32, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "A"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "B/C/D"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 6, 29, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "R32-2", SlotKey = "R32-2", Round = Round.R32, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "C"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "D"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 6, 29, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "R32-3", SlotKey = "R32-3", Round = Round.R32, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "B"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "A/C/E"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 6, 30, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Estadio Azteca", City = "Mexico City"
        },
        new()
        {
            Id = "R32-4", SlotKey = "R32-4", Round = Round.R32, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "A"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "B"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 6, 30, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "R32-5", SlotKey = "R32-5", Round = Round.R32, SlotNumber = 5,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "C"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "A/B/F"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 1, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },
        new()
        {
            Id = "R32-6", SlotKey = "R32-6", Round = Round.R32, SlotNumber = 6,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "E"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "F"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 1, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R32-7", SlotKey = "R32-7", Round = Round.R32, SlotNumber = 7,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "D"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "G/H/I"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 2, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },
        new()
        {
            Id = "R32-8", SlotKey = "R32-8", Round = Round.R32, SlotNumber = 8,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "G"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "H"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 2, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },
        new()
        {
            Id = "R32-9", SlotKey = "R32-9", Round = Round.R32, SlotNumber = 9,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "E"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "J/K/L"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 3, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "R32-10", SlotKey = "R32-10", Round = Round.R32, SlotNumber = 10,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "I"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "J"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 3, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },
        new()
        {
            Id = "R32-11", SlotKey = "R32-11", Round = Round.R32, SlotNumber = 11,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "F"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "D/E/I"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 4, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R32-12", SlotKey = "R32-12", Round = Round.R32, SlotNumber = 12,
            HomeSlotSource = Src(SlotSourceType.GroupRunnerUp, "K"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp, "L"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 4, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },
        new()
        {
            Id = "R32-13", SlotKey = "R32-13", Round = Round.R32, SlotNumber = 13,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "G"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "A/B/H"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 5, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "R32-14", SlotKey = "R32-14", Round = Round.R32, SlotNumber = 14,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "I"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "L"), // placeholder // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 5, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },
        new()
        {
            Id = "R32-15", SlotKey = "R32-15", Round = Round.R32, SlotNumber = 15,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "H"),
            AwaySlotSource = Src(SlotSourceType.BestThirdPlace, "C/F/G"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 6, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },
        new()
        {
            Id = "R32-16", SlotKey = "R32-16", Round = Round.R32, SlotNumber = 16,
            HomeSlotSource = Src(SlotSourceType.GroupWinner,    "J"),
            AwaySlotSource = Src(SlotSourceType.GroupRunnerUp,  "K"), // TODO: verify bracket wiring
            KickoffUtc = new DateTimeOffset(2026, 7, 6, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },

        // ═══════════════════════════════════════════════════════════════════
        // ROUND OF 16  (8 matches)
        // Winners of paired R32 matches advance.
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "R16-1", SlotKey = "R16-1", Round = Round.R16, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 10, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "R16-2", SlotKey = "R16-2", Round = Round.R16, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 10, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "R16-3", SlotKey = "R16-3", Round = Round.R16, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-5"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-6"),
            KickoffUtc = new DateTimeOffset(2026, 7, 11, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "R16-4", SlotKey = "R16-4", Round = Round.R16, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-7"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-8"),
            KickoffUtc = new DateTimeOffset(2026, 7, 11, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },
        new()
        {
            Id = "R16-5", SlotKey = "R16-5", Round = Round.R16, SlotNumber = 5,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-9"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-10"),
            KickoffUtc = new DateTimeOffset(2026, 7, 12, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "R16-6", SlotKey = "R16-6", Round = Round.R16, SlotNumber = 6,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-11"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-12"),
            KickoffUtc = new DateTimeOffset(2026, 7, 12, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "R16-7", SlotKey = "R16-7", Round = Round.R16, SlotNumber = 7,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-13"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-14"),
            KickoffUtc = new DateTimeOffset(2026, 7, 13, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },
        new()
        {
            Id = "R16-8", SlotKey = "R16-8", Round = Round.R16, SlotNumber = 8,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R32-15"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R32-16"),
            KickoffUtc = new DateTimeOffset(2026, 7, 13, 23, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },

        // ═══════════════════════════════════════════════════════════════════
        // QUARTER-FINALS  (4 matches)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "QF-1", SlotKey = "QF-1", Round = Round.QF, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 17, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "QF-2", SlotKey = "QF-2", Round = Round.QF, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 18, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "QF-3", SlotKey = "QF-3", Round = Round.QF, SlotNumber = 3,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-5"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-6"),
            KickoffUtc = new DateTimeOffset(2026, 7, 19, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "QF-4", SlotKey = "QF-4", Round = Round.QF, SlotNumber = 4,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "R16-7"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "R16-8"),
            KickoffUtc = new DateTimeOffset(2026, 7, 20, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },

        // ═══════════════════════════════════════════════════════════════════
        // SEMI-FINALS  (2 matches)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "SF-1", SlotKey = "SF-1", Round = Round.SF, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "QF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "QF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "SF-2", SlotKey = "SF-2", Round = Round.SF, SlotNumber = 2,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "QF-3"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "QF-4"),
            KickoffUtc = new DateTimeOffset(2026, 7, 25, 0, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },

        // ═══════════════════════════════════════════════════════════════════
        // THIRD-PLACE PLAY-OFF  (1 match)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "3RD", SlotKey = "3RD", Round = Round.ThirdPlace, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchLoser, "SF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchLoser, "SF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 28, 20, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },

        // ═══════════════════════════════════════════════════════════════════
        // FINAL  (1 match)
        // ═══════════════════════════════════════════════════════════════════
        new()
        {
            Id = "FIN", SlotKey = "FIN", Round = Round.Final, SlotNumber = 1,
            HomeSlotSource = Src(SlotSourceType.MatchWinner, "SF-1"),
            AwaySlotSource = Src(SlotSourceType.MatchWinner, "SF-2"),
            KickoffUtc = new DateTimeOffset(2026, 7, 29, 22, 0, 0, TimeSpan.Zero), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
    ];
}
