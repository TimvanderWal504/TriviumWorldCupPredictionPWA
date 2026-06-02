using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// All 72 group-stage fixtures for FIFA World Cup 2026.
/// The group stage runs 11–27 June 2026.
/// Kickoff times are stored in UTC.
///
/// The official FIFA match schedule was not finalised when this seed was written.
/// Times marked // TODO: verify kickoff time should be validated against the
/// official FIFA schedule before production use.
/// Venue assignments are approximate based on the known host city allocations.
///
/// Match numbering follows FIFA's official numbering (1–72 for group stage).
/// Where the exact FIFA number is uncertain, a sequential number is assigned.
/// </summary>
public static class FixturesData
{
    public static IReadOnlyList<Fixture> All => _all;

    // Helper to create a UTC DateTimeOffset
    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0)
        => new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static readonly List<Fixture> _all =
    [
        // ═══════════════════════════════════════════════════════════════════
        // GROUP A  (USA, URU, PAN, BOL)
        // Venues: MetLife Stadium (East Rutherford NJ), SoFi Stadium (Los Angeles),
        //         AT&T Stadium (Dallas), Levi's Stadium (San Francisco),
        //         Allegiant Stadium (Las Vegas)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "1", MatchNumber = 1, GroupLetter = "A",
            HomeTeamId = "USA", AwayTeamId = "URU",
            KickoffUtc = Utc(2026, 6, 11, 21, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "2", MatchNumber = 2, GroupLetter = "A",
            HomeTeamId = "PAN", AwayTeamId = "BOL",
            KickoffUtc = Utc(2026, 6, 12, 0, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },

        // Matchday 2
        new()
        {
            Id = "3", MatchNumber = 3, GroupLetter = "A",
            HomeTeamId = "USA", AwayTeamId = "BOL",
            KickoffUtc = Utc(2026, 6, 15, 21, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "4", MatchNumber = 4, GroupLetter = "A",
            HomeTeamId = "URU", AwayTeamId = "PAN",
            KickoffUtc = Utc(2026, 6, 16, 0, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },

        // Matchday 3
        new()
        {
            Id = "5", MatchNumber = 5, GroupLetter = "A",
            HomeTeamId = "URU", AwayTeamId = "BOL",
            KickoffUtc = Utc(2026, 6, 19, 22, 0), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },
        new()
        {
            Id = "6", MatchNumber = 6, GroupLetter = "A",
            HomeTeamId = "USA", AwayTeamId = "PAN",
            KickoffUtc = Utc(2026, 6, 19, 22, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP B  (MEX, ARG, NZL, JAM)
        // Venues: Estadio Azteca (Mexico City), Estadio Akron (Guadalajara),
        //         Estadio BBVA (Monterrey)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "7", MatchNumber = 7, GroupLetter = "B",
            HomeTeamId = "MEX", AwayTeamId = "ARG",
            KickoffUtc = Utc(2026, 6, 11, 0, 0), // TODO: verify kickoff time
            Venue = "Estadio Azteca", City = "Mexico City"
        },
        new()
        {
            Id = "8", MatchNumber = 8, GroupLetter = "B",
            HomeTeamId = "NZL", AwayTeamId = "JAM",
            KickoffUtc = Utc(2026, 6, 12, 3, 0), // TODO: verify kickoff time
            Venue = "Estadio Akron", City = "Guadalajara"
        },

        // Matchday 2
        new()
        {
            Id = "9", MatchNumber = 9, GroupLetter = "B",
            HomeTeamId = "MEX", AwayTeamId = "JAM",
            KickoffUtc = Utc(2026, 6, 15, 3, 0), // TODO: verify kickoff time
            Venue = "Estadio Azteca", City = "Mexico City"
        },
        new()
        {
            Id = "10", MatchNumber = 10, GroupLetter = "B",
            HomeTeamId = "ARG", AwayTeamId = "NZL",
            KickoffUtc = Utc(2026, 6, 15, 0, 0), // TODO: verify kickoff time
            Venue = "Estadio BBVA", City = "Monterrey"
        },

        // Matchday 3
        new()
        {
            Id = "11", MatchNumber = 11, GroupLetter = "B",
            HomeTeamId = "ARG", AwayTeamId = "JAM",
            KickoffUtc = Utc(2026, 6, 19, 3, 0), // TODO: verify kickoff time
            Venue = "Estadio Akron", City = "Guadalajara"
        },
        new()
        {
            Id = "12", MatchNumber = 12, GroupLetter = "B",
            HomeTeamId = "MEX", AwayTeamId = "NZL",
            KickoffUtc = Utc(2026, 6, 19, 3, 0), // TODO: verify kickoff time
            Venue = "Estadio Azteca", City = "Mexico City"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP C  (CAN, MAR, ECU, TUN)
        // Venues: BC Place (Vancouver), BMO Field (Toronto)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "13", MatchNumber = 13, GroupLetter = "C",
            HomeTeamId = "CAN", AwayTeamId = "MAR",
            KickoffUtc = Utc(2026, 6, 12, 20, 0), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },
        new()
        {
            Id = "14", MatchNumber = 14, GroupLetter = "C",
            HomeTeamId = "ECU", AwayTeamId = "TUN",
            KickoffUtc = Utc(2026, 6, 13, 0, 0), // TODO: verify kickoff time
            Venue = "BMO Field", City = "Toronto"
        },

        // Matchday 2
        new()
        {
            Id = "15", MatchNumber = 15, GroupLetter = "C",
            HomeTeamId = "CAN", AwayTeamId = "TUN",
            KickoffUtc = Utc(2026, 6, 16, 20, 0), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },
        new()
        {
            Id = "16", MatchNumber = 16, GroupLetter = "C",
            HomeTeamId = "MAR", AwayTeamId = "ECU",
            KickoffUtc = Utc(2026, 6, 17, 0, 0), // TODO: verify kickoff time
            Venue = "BMO Field", City = "Toronto"
        },

        // Matchday 3
        new()
        {
            Id = "17", MatchNumber = 17, GroupLetter = "C",
            HomeTeamId = "MAR", AwayTeamId = "TUN",
            KickoffUtc = Utc(2026, 6, 20, 22, 0), // TODO: verify kickoff time
            Venue = "BC Place", City = "Vancouver"
        },
        new()
        {
            Id = "18", MatchNumber = 18, GroupLetter = "C",
            HomeTeamId = "CAN", AwayTeamId = "ECU",
            KickoffUtc = Utc(2026, 6, 20, 22, 0), // TODO: verify kickoff time
            Venue = "BMO Field", City = "Toronto"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP D  (BRA, GER, JPN, CMR)
        // Venues: SoFi Stadium (LA), Rose Bowl (Pasadena), Levi's Stadium (SF)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "19", MatchNumber = 19, GroupLetter = "D",
            HomeTeamId = "BRA", AwayTeamId = "GER",
            KickoffUtc = Utc(2026, 6, 13, 20, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "20", MatchNumber = 20, GroupLetter = "D",
            HomeTeamId = "JPN", AwayTeamId = "CMR",
            KickoffUtc = Utc(2026, 6, 13, 23, 0), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },

        // Matchday 2
        new()
        {
            Id = "21", MatchNumber = 21, GroupLetter = "D",
            HomeTeamId = "BRA", AwayTeamId = "CMR",
            KickoffUtc = Utc(2026, 6, 17, 20, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "22", MatchNumber = 22, GroupLetter = "D",
            HomeTeamId = "GER", AwayTeamId = "JPN",
            KickoffUtc = Utc(2026, 6, 17, 23, 0), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },

        // Matchday 3
        new()
        {
            Id = "23", MatchNumber = 23, GroupLetter = "D",
            HomeTeamId = "GER", AwayTeamId = "CMR",
            KickoffUtc = Utc(2026, 6, 21, 22, 0), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },
        new()
        {
            Id = "24", MatchNumber = 24, GroupLetter = "D",
            HomeTeamId = "BRA", AwayTeamId = "JPN",
            KickoffUtc = Utc(2026, 6, 21, 22, 0), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP E  (ESP, SRB, COL, NGR)
        // Venues: MetLife Stadium, Hard Rock Stadium (Miami), Lincoln Financial Field (Philly)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "25", MatchNumber = 25, GroupLetter = "E",
            HomeTeamId = "ESP", AwayTeamId = "SRB",
            KickoffUtc = Utc(2026, 6, 14, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "26", MatchNumber = 26, GroupLetter = "E",
            HomeTeamId = "COL", AwayTeamId = "NGR",
            KickoffUtc = Utc(2026, 6, 14, 19, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },

        // Matchday 2
        new()
        {
            Id = "27", MatchNumber = 27, GroupLetter = "E",
            HomeTeamId = "ESP", AwayTeamId = "NGR",
            KickoffUtc = Utc(2026, 6, 18, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "28", MatchNumber = 28, GroupLetter = "E",
            HomeTeamId = "SRB", AwayTeamId = "COL",
            KickoffUtc = Utc(2026, 6, 18, 19, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },

        // Matchday 3
        new()
        {
            Id = "29", MatchNumber = 29, GroupLetter = "E",
            HomeTeamId = "SRB", AwayTeamId = "NGR",
            KickoffUtc = Utc(2026, 6, 22, 22, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },
        new()
        {
            Id = "30", MatchNumber = 30, GroupLetter = "E",
            HomeTeamId = "ESP", AwayTeamId = "COL",
            KickoffUtc = Utc(2026, 6, 22, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP F  (POR, CRO, SEN, IRI)
        // Venues: AT&T Stadium (Dallas), Arrowhead Stadium (KC), Gillette Stadium (Boston)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "31", MatchNumber = 31, GroupLetter = "F",
            HomeTeamId = "POR", AwayTeamId = "CRO",
            KickoffUtc = Utc(2026, 6, 14, 2, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "32", MatchNumber = 32, GroupLetter = "F",
            HomeTeamId = "SEN", AwayTeamId = "IRI",
            KickoffUtc = Utc(2026, 6, 15, 2, 0), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // Matchday 2
        new()
        {
            Id = "33", MatchNumber = 33, GroupLetter = "F",
            HomeTeamId = "POR", AwayTeamId = "IRI",
            KickoffUtc = Utc(2026, 6, 18, 2, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "34", MatchNumber = 34, GroupLetter = "F",
            HomeTeamId = "CRO", AwayTeamId = "SEN",
            KickoffUtc = Utc(2026, 6, 18, 20, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },

        // Matchday 3
        new()
        {
            Id = "35", MatchNumber = 35, GroupLetter = "F",
            HomeTeamId = "CRO", AwayTeamId = "IRI",
            KickoffUtc = Utc(2026, 6, 22, 2, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "36", MatchNumber = 36, GroupLetter = "F",
            HomeTeamId = "POR", AwayTeamId = "SEN",
            KickoffUtc = Utc(2026, 6, 22, 2, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP G  (ENG, FRA, AUS, VEN)
        // Venues: MetLife Stadium, Gillette Stadium, Lincoln Financial Field
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "37", MatchNumber = 37, GroupLetter = "G",
            HomeTeamId = "ENG", AwayTeamId = "FRA",
            KickoffUtc = Utc(2026, 6, 14, 17, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "38", MatchNumber = 38, GroupLetter = "G",
            HomeTeamId = "AUS", AwayTeamId = "VEN",
            KickoffUtc = Utc(2026, 6, 15, 17, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },

        // Matchday 2
        new()
        {
            Id = "39", MatchNumber = 39, GroupLetter = "G",
            HomeTeamId = "ENG", AwayTeamId = "VEN",
            KickoffUtc = Utc(2026, 6, 18, 17, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "40", MatchNumber = 40, GroupLetter = "G",
            HomeTeamId = "FRA", AwayTeamId = "AUS",
            KickoffUtc = Utc(2026, 6, 19, 17, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },

        // Matchday 3
        new()
        {
            Id = "41", MatchNumber = 41, GroupLetter = "G",
            HomeTeamId = "FRA", AwayTeamId = "VEN",
            KickoffUtc = Utc(2026, 6, 23, 22, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },
        new()
        {
            Id = "42", MatchNumber = 42, GroupLetter = "G",
            HomeTeamId = "ENG", AwayTeamId = "AUS",
            KickoffUtc = Utc(2026, 6, 23, 22, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP H  (NED, NOR, CIV, CHI)
        // Venues: Hard Rock Stadium (Miami), SoFi Stadium (LA), Allegiant (Las Vegas)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "43", MatchNumber = 43, GroupLetter = "H",
            HomeTeamId = "NED", AwayTeamId = "NOR",
            KickoffUtc = Utc(2026, 6, 13, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "44", MatchNumber = 44, GroupLetter = "H",
            HomeTeamId = "CIV", AwayTeamId = "CHI",
            KickoffUtc = Utc(2026, 6, 13, 19, 0), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },

        // Matchday 2
        new()
        {
            Id = "45", MatchNumber = 45, GroupLetter = "H",
            HomeTeamId = "NED", AwayTeamId = "CHI",
            KickoffUtc = Utc(2026, 6, 17, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "46", MatchNumber = 46, GroupLetter = "H",
            HomeTeamId = "NOR", AwayTeamId = "CIV",
            KickoffUtc = Utc(2026, 6, 17, 19, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },

        // Matchday 3
        new()
        {
            Id = "47", MatchNumber = 47, GroupLetter = "H",
            HomeTeamId = "NOR", AwayTeamId = "CHI",
            KickoffUtc = Utc(2026, 6, 21, 2, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "48", MatchNumber = 48, GroupLetter = "H",
            HomeTeamId = "NED", AwayTeamId = "CIV",
            KickoffUtc = Utc(2026, 6, 21, 2, 0), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP I  (BEL, TUR, GHA, KSA)
        // Venues: AT&T Stadium (Dallas), Arrowhead (KC), Levi's (SF)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "49", MatchNumber = 49, GroupLetter = "I",
            HomeTeamId = "BEL", AwayTeamId = "TUR",
            KickoffUtc = Utc(2026, 6, 14, 20, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "50", MatchNumber = 50, GroupLetter = "I",
            HomeTeamId = "GHA", AwayTeamId = "KSA",
            KickoffUtc = Utc(2026, 6, 14, 23, 0), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },

        // Matchday 2
        new()
        {
            Id = "51", MatchNumber = 51, GroupLetter = "I",
            HomeTeamId = "BEL", AwayTeamId = "KSA",
            KickoffUtc = Utc(2026, 6, 18, 20, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "52", MatchNumber = 52, GroupLetter = "I",
            HomeTeamId = "TUR", AwayTeamId = "GHA",
            KickoffUtc = Utc(2026, 6, 19, 20, 0), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // Matchday 3
        new()
        {
            Id = "53", MatchNumber = 53, GroupLetter = "I",
            HomeTeamId = "TUR", AwayTeamId = "KSA",
            KickoffUtc = Utc(2026, 6, 23, 2, 0), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },
        new()
        {
            Id = "54", MatchNumber = 54, GroupLetter = "I",
            HomeTeamId = "BEL", AwayTeamId = "GHA",
            KickoffUtc = Utc(2026, 6, 23, 2, 0), // TODO: verify kickoff time
            Venue = "Levi's Stadium", City = "San Francisco"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP J  (KOR, AUT, QAT, ALG)
        // Venues: Rose Bowl (Pasadena), SoFi (LA), Allegiant (Las Vegas)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "55", MatchNumber = 55, GroupLetter = "J",
            HomeTeamId = "KOR", AwayTeamId = "AUT",
            KickoffUtc = Utc(2026, 6, 15, 20, 0), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },
        new()
        {
            Id = "56", MatchNumber = 56, GroupLetter = "J",
            HomeTeamId = "QAT", AwayTeamId = "ALG",
            KickoffUtc = Utc(2026, 6, 16, 2, 0), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },

        // Matchday 2
        new()
        {
            Id = "57", MatchNumber = 57, GroupLetter = "J",
            HomeTeamId = "KOR", AwayTeamId = "ALG",
            KickoffUtc = Utc(2026, 6, 20, 0, 0), // TODO: verify kickoff time
            Venue = "Rose Bowl", City = "Pasadena"
        },
        new()
        {
            Id = "58", MatchNumber = 58, GroupLetter = "J",
            HomeTeamId = "AUT", AwayTeamId = "QAT",
            KickoffUtc = Utc(2026, 6, 20, 3, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },

        // Matchday 3
        new()
        {
            Id = "59", MatchNumber = 59, GroupLetter = "J",
            HomeTeamId = "AUT", AwayTeamId = "ALG",
            KickoffUtc = Utc(2026, 6, 24, 2, 0), // TODO: verify kickoff time
            Venue = "SoFi Stadium", City = "Los Angeles"
        },
        new()
        {
            Id = "60", MatchNumber = 60, GroupLetter = "J",
            HomeTeamId = "KOR", AwayTeamId = "QAT",
            KickoffUtc = Utc(2026, 6, 24, 2, 0), // TODO: verify kickoff time
            Venue = "Allegiant Stadium", City = "Las Vegas"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP K  (POL, SUI, RSA, EGY)
        // Venues: Gillette Stadium (Boston), Arrowhead (KC), MetLife
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "61", MatchNumber = 61, GroupLetter = "K",
            HomeTeamId = "POL", AwayTeamId = "SUI",
            KickoffUtc = Utc(2026, 6, 16, 17, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "62", MatchNumber = 62, GroupLetter = "K",
            HomeTeamId = "RSA", AwayTeamId = "EGY",
            KickoffUtc = Utc(2026, 6, 16, 20, 0), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // Matchday 2
        new()
        {
            Id = "63", MatchNumber = 63, GroupLetter = "K",
            HomeTeamId = "POL", AwayTeamId = "EGY",
            KickoffUtc = Utc(2026, 6, 20, 17, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },
        new()
        {
            Id = "64", MatchNumber = 64, GroupLetter = "K",
            HomeTeamId = "SUI", AwayTeamId = "RSA",
            KickoffUtc = Utc(2026, 6, 20, 20, 0), // TODO: verify kickoff time
            Venue = "Arrowhead Stadium", City = "Kansas City"
        },

        // Matchday 3
        new()
        {
            Id = "65", MatchNumber = 65, GroupLetter = "K",
            HomeTeamId = "SUI", AwayTeamId = "EGY",
            KickoffUtc = Utc(2026, 6, 24, 22, 0), // TODO: verify kickoff time
            Venue = "MetLife Stadium", City = "East Rutherford"
        },
        new()
        {
            Id = "66", MatchNumber = 66, GroupLetter = "K",
            HomeTeamId = "POL", AwayTeamId = "RSA",
            KickoffUtc = Utc(2026, 6, 24, 22, 0), // TODO: verify kickoff time
            Venue = "Gillette Stadium", City = "Boston"
        },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP L  (PER, UZB, IRQ, PAR)
        // Venues: Hard Rock Stadium (Miami), Lincoln Financial (Philly), AT&T (Dallas)
        // ═══════════════════════════════════════════════════════════════════

        // Matchday 1
        new()
        {
            Id = "67", MatchNumber = 67, GroupLetter = "L",
            HomeTeamId = "PER", AwayTeamId = "UZB",
            KickoffUtc = Utc(2026, 6, 16, 23, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },
        new()
        {
            Id = "68", MatchNumber = 68, GroupLetter = "L",
            HomeTeamId = "IRQ", AwayTeamId = "PAR",
            KickoffUtc = Utc(2026, 6, 17, 2, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },

        // Matchday 2
        new()
        {
            Id = "69", MatchNumber = 69, GroupLetter = "L",
            HomeTeamId = "PER", AwayTeamId = "PAR",
            KickoffUtc = Utc(2026, 6, 21, 19, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "70", MatchNumber = 70, GroupLetter = "L",
            HomeTeamId = "UZB", AwayTeamId = "IRQ",
            KickoffUtc = Utc(2026, 6, 21, 22, 0), // TODO: verify kickoff time
            Venue = "Hard Rock Stadium", City = "Miami"
        },

        // Matchday 3
        new()
        {
            Id = "71", MatchNumber = 71, GroupLetter = "L",
            HomeTeamId = "UZB", AwayTeamId = "PAR",
            KickoffUtc = Utc(2026, 6, 25, 2, 0), // TODO: verify kickoff time
            Venue = "AT&T Stadium", City = "Dallas"
        },
        new()
        {
            Id = "72", MatchNumber = 72, GroupLetter = "L",
            HomeTeamId = "PER", AwayTeamId = "IRQ",
            KickoffUtc = Utc(2026, 6, 25, 2, 0), // TODO: verify kickoff time
            Venue = "Lincoln Financial Field", City = "Philadelphia"
        },
    ];
}
