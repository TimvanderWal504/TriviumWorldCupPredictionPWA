using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// All 72 group-stage fixtures for FIFA World Cup 2026.
/// Source: Wikipedia individual group pages, verified June 2026.
/// All kickoff times stored in UTC.
/// Times converted from local timezone where noted:
///   Mexico venues (Mexico City/Zapopan/Guadalupe) = UTC-6 (CST, Mexico abolished DST in 2022)
///   US Eastern venues (East Rutherford/Foxborough/Philadelphia/Miami/Atlanta/Toronto) = UTC-4 (EDT)
///   US Central venues (Houston/Kansas City/Arlington/Dallas) = UTC-5 (CDT)
///   US Pacific venues (Inglewood/Santa Clara/Seattle/Vancouver) = UTC-7 (PDT)
/// </summary>
public static class FixturesData
{
    public static IReadOnlyList<Fixture> All => _all;

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute = 0)
        => new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private static readonly List<Fixture> _all =
    [
        // ═══════════════════════════════════════════════════════════════════
        // GROUP A  — MEX, RSA, KOR, CZE
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "1",  MatchNumber = 1,  GroupLetter = "A", HomeTeamId = "MEX", AwayTeamId = "RSA", KickoffUtc = Utc(2026, 6, 11, 19,  0), Venue = "Estadio Azteca",    City = "Mexico City"       }, // opening match; 1 PM UTC-6
        new() { Id = "2",  MatchNumber = 2,  GroupLetter = "A", HomeTeamId = "KOR", AwayTeamId = "CZE", KickoffUtc = Utc(2026, 6, 12,  2,  0), Venue = "Estadio Akron",     City = "Zapopan"           }, // 8 PM UTC-6 Jun 11
        new() { Id = "3",  MatchNumber = 3,  GroupLetter = "A", HomeTeamId = "CZE", AwayTeamId = "RSA", KickoffUtc = Utc(2026, 6, 18, 16,  0), Venue = "Mercedes-Benz Stadium", City = "Atlanta"       }, // 12 PM UTC-4
        new() { Id = "4",  MatchNumber = 4,  GroupLetter = "A", HomeTeamId = "MEX", AwayTeamId = "KOR", KickoffUtc = Utc(2026, 6, 19,  1,  0), Venue = "Estadio Akron",     City = "Zapopan"           }, // 7 PM UTC-6 Jun 18
        new() { Id = "5",  MatchNumber = 5,  GroupLetter = "A", HomeTeamId = "CZE", AwayTeamId = "MEX", KickoffUtc = Utc(2026, 6, 25,  1,  0), Venue = "Estadio Azteca",    City = "Mexico City"       }, // 7 PM UTC-6 Jun 24
        new() { Id = "6",  MatchNumber = 6,  GroupLetter = "A", HomeTeamId = "RSA", AwayTeamId = "KOR", KickoffUtc = Utc(2026, 6, 25,  1,  0), Venue = "Estadio BBVA",      City = "Guadalupe"         }, // 7 PM UTC-6 Jun 24

        // ═══════════════════════════════════════════════════════════════════
        // GROUP B  — CAN, BIH, QAT, SUI
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "7",  MatchNumber = 7,  GroupLetter = "B", HomeTeamId = "CAN", AwayTeamId = "BIH", KickoffUtc = Utc(2026, 6, 12, 19,  0), Venue = "BMO Field",         City = "Toronto"           },
        new() { Id = "8",  MatchNumber = 8,  GroupLetter = "B", HomeTeamId = "QAT", AwayTeamId = "SUI", KickoffUtc = Utc(2026, 6, 13, 19,  0), Venue = "Levi's Stadium",    City = "Santa Clara"       },
        new() { Id = "9",  MatchNumber = 9,  GroupLetter = "B", HomeTeamId = "SUI", AwayTeamId = "BIH", KickoffUtc = Utc(2026, 6, 18, 19,  0), Venue = "SoFi Stadium",      City = "Inglewood"         },
        new() { Id = "10", MatchNumber = 10, GroupLetter = "B", HomeTeamId = "CAN", AwayTeamId = "QAT", KickoffUtc = Utc(2026, 6, 18, 22,  0), Venue = "BC Place",          City = "Vancouver"         },
        new() { Id = "11", MatchNumber = 11, GroupLetter = "B", HomeTeamId = "SUI", AwayTeamId = "CAN", KickoffUtc = Utc(2026, 6, 24, 19,  0), Venue = "BC Place",          City = "Vancouver"         },
        new() { Id = "12", MatchNumber = 12, GroupLetter = "B", HomeTeamId = "BIH", AwayTeamId = "QAT", KickoffUtc = Utc(2026, 6, 24, 19,  0), Venue = "Lumen Field",       City = "Seattle"           },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP C  — BRA, MAR, HTI, SCO
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "13", MatchNumber = 13, GroupLetter = "C", HomeTeamId = "BRA", AwayTeamId = "MAR", KickoffUtc = Utc(2026, 6, 13, 22,  0), Venue = "MetLife Stadium",   City = "East Rutherford"   },
        new() { Id = "14", MatchNumber = 14, GroupLetter = "C", HomeTeamId = "HTI", AwayTeamId = "SCO", KickoffUtc = Utc(2026, 6, 14,  1,  0), Venue = "Gillette Stadium",  City = "Foxborough"        }, // 9 PM UTC-4 Jun 13
        new() { Id = "15", MatchNumber = 15, GroupLetter = "C", HomeTeamId = "SCO", AwayTeamId = "MAR", KickoffUtc = Utc(2026, 6, 19, 22,  0), Venue = "Gillette Stadium",  City = "Foxborough"        },
        new() { Id = "16", MatchNumber = 16, GroupLetter = "C", HomeTeamId = "BRA", AwayTeamId = "HTI", KickoffUtc = Utc(2026, 6, 20,  0, 30), Venue = "Lincoln Financial Field", City = "Philadelphia" }, // 12:30 AM UTC Jun 20
        new() { Id = "17", MatchNumber = 17, GroupLetter = "C", HomeTeamId = "SCO", AwayTeamId = "BRA", KickoffUtc = Utc(2026, 6, 24, 22,  0), Venue = "Hard Rock Stadium", City = "Miami Gardens"     },
        new() { Id = "18", MatchNumber = 18, GroupLetter = "C", HomeTeamId = "MAR", AwayTeamId = "HTI", KickoffUtc = Utc(2026, 6, 25,  0,  0), Venue = "Mercedes-Benz Stadium", City = "Atlanta"       },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP D  — USA, PAR, AUS, TUR
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "19", MatchNumber = 19, GroupLetter = "D", HomeTeamId = "USA", AwayTeamId = "PAR", KickoffUtc = Utc(2026, 6, 13,  1,  0), Venue = "SoFi Stadium",      City = "Inglewood"         }, // 6 PM UTC-7 Jun 12
        new() { Id = "20", MatchNumber = 20, GroupLetter = "D", HomeTeamId = "AUS", AwayTeamId = "TUR", KickoffUtc = Utc(2026, 6, 14,  4,  0), Venue = "BC Place",          City = "Vancouver"         }, // 9 PM UTC-7 Jun 13
        new() { Id = "21", MatchNumber = 21, GroupLetter = "D", HomeTeamId = "USA", AwayTeamId = "AUS", KickoffUtc = Utc(2026, 6, 19, 19,  0), Venue = "Lumen Field",       City = "Seattle"           }, // 12 PM UTC-7
        new() { Id = "22", MatchNumber = 22, GroupLetter = "D", HomeTeamId = "TUR", AwayTeamId = "PAR", KickoffUtc = Utc(2026, 6, 20,  3,  0), Venue = "Levi's Stadium",    City = "Santa Clara"       }, // 8 PM UTC-7 Jun 19
        new() { Id = "23", MatchNumber = 23, GroupLetter = "D", HomeTeamId = "TUR", AwayTeamId = "USA", KickoffUtc = Utc(2026, 6, 26,  2,  0), Venue = "SoFi Stadium",      City = "Inglewood"         }, // 7 PM UTC-7 Jun 25
        new() { Id = "24", MatchNumber = 24, GroupLetter = "D", HomeTeamId = "PAR", AwayTeamId = "AUS", KickoffUtc = Utc(2026, 6, 26,  2,  0), Venue = "Levi's Stadium",    City = "Santa Clara"       }, // 7 PM UTC-7 Jun 25

        // ═══════════════════════════════════════════════════════════════════
        // GROUP E  — GER, CUW, CIV, ECU
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "25", MatchNumber = 25, GroupLetter = "E", HomeTeamId = "GER", AwayTeamId = "CUW", KickoffUtc = Utc(2026, 6, 14, 16,  0), Venue = "NRG Stadium",       City = "Houston"           },
        new() { Id = "26", MatchNumber = 26, GroupLetter = "E", HomeTeamId = "CIV", AwayTeamId = "ECU", KickoffUtc = Utc(2026, 6, 14, 23,  0), Venue = "Lincoln Financial Field", City = "Philadelphia" },
        new() { Id = "27", MatchNumber = 27, GroupLetter = "E", HomeTeamId = "GER", AwayTeamId = "CIV", KickoffUtc = Utc(2026, 6, 20, 20,  0), Venue = "BMO Field",         City = "Toronto"           },
        new() { Id = "28", MatchNumber = 28, GroupLetter = "E", HomeTeamId = "ECU", AwayTeamId = "CUW", KickoffUtc = Utc(2026, 6, 20, 23,  0), Venue = "Arrowhead Stadium", City = "Kansas City"       },
        new() { Id = "29", MatchNumber = 29, GroupLetter = "E", HomeTeamId = "CUW", AwayTeamId = "CIV", KickoffUtc = Utc(2026, 6, 25, 20,  0), Venue = "Lincoln Financial Field", City = "Philadelphia" },
        new() { Id = "30", MatchNumber = 30, GroupLetter = "E", HomeTeamId = "ECU", AwayTeamId = "GER", KickoffUtc = Utc(2026, 6, 25, 20,  0), Venue = "MetLife Stadium",   City = "East Rutherford"   },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP F  — NED, JPN, SWE, TUN
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "31", MatchNumber = 31, GroupLetter = "F", HomeTeamId = "NED", AwayTeamId = "JPN", KickoffUtc = Utc(2026, 6, 14, 20,  0), Venue = "AT&T Stadium",      City = "Arlington"         },
        new() { Id = "32", MatchNumber = 32, GroupLetter = "F", HomeTeamId = "SWE", AwayTeamId = "TUN", KickoffUtc = Utc(2026, 6, 14,  2,  0), Venue = "Estadio BBVA",      City = "Guadalupe"         }, // 8 PM UTC-6 Jun 13
        new() { Id = "33", MatchNumber = 33, GroupLetter = "F", HomeTeamId = "NED", AwayTeamId = "SWE", KickoffUtc = Utc(2026, 6, 20, 16,  0), Venue = "NRG Stadium",       City = "Houston"           },
        new() { Id = "34", MatchNumber = 34, GroupLetter = "F", HomeTeamId = "TUN", AwayTeamId = "JPN", KickoffUtc = Utc(2026, 6, 20,  4,  0), Venue = "Estadio BBVA",      City = "Guadalupe"         }, // 10 PM UTC-6 Jun 19
        new() { Id = "35", MatchNumber = 35, GroupLetter = "F", HomeTeamId = "JPN", AwayTeamId = "SWE", KickoffUtc = Utc(2026, 6, 25, 23,  0), Venue = "AT&T Stadium",      City = "Arlington"         },
        new() { Id = "36", MatchNumber = 36, GroupLetter = "F", HomeTeamId = "TUN", AwayTeamId = "NED", KickoffUtc = Utc(2026, 6, 25, 23,  0), Venue = "Arrowhead Stadium", City = "Kansas City"       },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP G  — BEL, EGY, IRN, NZL
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "37", MatchNumber = 37, GroupLetter = "G", HomeTeamId = "BEL", AwayTeamId = "EGY", KickoffUtc = Utc(2026, 6, 16,  2,  0), Venue = "Lumen Field",       City = "Seattle"           }, // 7 PM UTC-7 Jun 15
        new() { Id = "38", MatchNumber = 38, GroupLetter = "G", HomeTeamId = "IRN", AwayTeamId = "NZL", KickoffUtc = Utc(2026, 6, 16,  8,  0), Venue = "SoFi Stadium",      City = "Inglewood"         }, // 1 AM UTC-7 Jun 16
        new() { Id = "39", MatchNumber = 39, GroupLetter = "G", HomeTeamId = "BEL", AwayTeamId = "IRN", KickoffUtc = Utc(2026, 6, 22,  2,  0), Venue = "SoFi Stadium",      City = "Inglewood"         }, // 7 PM UTC-7 Jun 21
        new() { Id = "40", MatchNumber = 40, GroupLetter = "G", HomeTeamId = "NZL", AwayTeamId = "EGY", KickoffUtc = Utc(2026, 6, 22,  8,  0), Venue = "BC Place",          City = "Vancouver"         }, // 1 AM UTC-7 Jun 22
        new() { Id = "41", MatchNumber = 41, GroupLetter = "G", HomeTeamId = "EGY", AwayTeamId = "IRN", KickoffUtc = Utc(2026, 6, 26, 10,  0), Venue = "Lumen Field",       City = "Seattle"           }, // 3 AM UTC-7
        new() { Id = "42", MatchNumber = 42, GroupLetter = "G", HomeTeamId = "NZL", AwayTeamId = "BEL", KickoffUtc = Utc(2026, 6, 26, 10,  0), Venue = "BC Place",          City = "Vancouver"         }, // 3 AM UTC-7

        // ═══════════════════════════════════════════════════════════════════
        // GROUP H  — ESP, CPV, KSA, URU
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "43", MatchNumber = 43, GroupLetter = "H", HomeTeamId = "ESP", AwayTeamId = "CPV", KickoffUtc = Utc(2026, 6, 15, 16,  0), Venue = "Mercedes-Benz Stadium", City = "Atlanta"       },
        new() { Id = "44", MatchNumber = 44, GroupLetter = "H", HomeTeamId = "KSA", AwayTeamId = "URU", KickoffUtc = Utc(2026, 6, 15, 22,  0), Venue = "Hard Rock Stadium", City = "Miami Gardens"     },
        new() { Id = "45", MatchNumber = 45, GroupLetter = "H", HomeTeamId = "ESP", AwayTeamId = "KSA", KickoffUtc = Utc(2026, 6, 21, 16,  0), Venue = "Mercedes-Benz Stadium", City = "Atlanta"       },
        new() { Id = "46", MatchNumber = 46, GroupLetter = "H", HomeTeamId = "URU", AwayTeamId = "CPV", KickoffUtc = Utc(2026, 6, 21, 22,  0), Venue = "Hard Rock Stadium", City = "Miami Gardens"     },
        new() { Id = "47", MatchNumber = 47, GroupLetter = "H", HomeTeamId = "CPV", AwayTeamId = "KSA", KickoffUtc = Utc(2026, 6, 26,  0,  0), Venue = "NRG Stadium",       City = "Houston"           }, // 7 PM UTC-5 Jun 25
        new() { Id = "48", MatchNumber = 48, GroupLetter = "H", HomeTeamId = "URU", AwayTeamId = "ESP", KickoffUtc = Utc(2026, 6, 26,  0,  0), Venue = "Estadio Akron",     City = "Zapopan"           }, // 6 PM UTC-6 Jun 25

        // ═══════════════════════════════════════════════════════════════════
        // GROUP I  — FRA, SEN, IRQ, NOR
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "49", MatchNumber = 49, GroupLetter = "I", HomeTeamId = "FRA", AwayTeamId = "SEN", KickoffUtc = Utc(2026, 6, 16, 19,  0), Venue = "MetLife Stadium",   City = "East Rutherford"   }, // 3 PM UTC-4
        new() { Id = "50", MatchNumber = 50, GroupLetter = "I", HomeTeamId = "IRQ", AwayTeamId = "NOR", KickoffUtc = Utc(2026, 6, 16, 22,  0), Venue = "Gillette Stadium",  City = "Foxborough"        }, // 6 PM UTC-4
        new() { Id = "51", MatchNumber = 51, GroupLetter = "I", HomeTeamId = "FRA", AwayTeamId = "IRQ", KickoffUtc = Utc(2026, 6, 22, 21,  0), Venue = "Lincoln Financial Field", City = "Philadelphia" }, // 5 PM UTC-4
        new() { Id = "52", MatchNumber = 52, GroupLetter = "I", HomeTeamId = "NOR", AwayTeamId = "SEN", KickoffUtc = Utc(2026, 6, 23,  0,  0), Venue = "MetLife Stadium",   City = "East Rutherford"   }, // 8 PM UTC-4 Jun 22
        new() { Id = "53", MatchNumber = 53, GroupLetter = "I", HomeTeamId = "NOR", AwayTeamId = "FRA", KickoffUtc = Utc(2026, 6, 26, 19,  0), Venue = "Gillette Stadium",  City = "Foxborough"        }, // 3 PM UTC-4
        new() { Id = "54", MatchNumber = 54, GroupLetter = "I", HomeTeamId = "SEN", AwayTeamId = "IRQ", KickoffUtc = Utc(2026, 6, 26, 19,  0), Venue = "BMO Field",         City = "Toronto"           }, // 3 PM UTC-4

        // ═══════════════════════════════════════════════════════════════════
        // GROUP J  — ARG, ALG, AUT, JOR
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "55", MatchNumber = 55, GroupLetter = "J", HomeTeamId = "ARG", AwayTeamId = "ALG", KickoffUtc = Utc(2026, 6, 16,  1,  0), Venue = "Arrowhead Stadium", City = "Kansas City"       }, // 8 PM UTC-5 Jun 15
        new() { Id = "56", MatchNumber = 56, GroupLetter = "J", HomeTeamId = "AUT", AwayTeamId = "JOR", KickoffUtc = Utc(2026, 6, 16,  4,  0), Venue = "Levi's Stadium",    City = "Santa Clara"       }, // 9 PM UTC-7 Jun 15
        new() { Id = "57", MatchNumber = 57, GroupLetter = "J", HomeTeamId = "ARG", AwayTeamId = "AUT", KickoffUtc = Utc(2026, 6, 22, 17,  0), Venue = "AT&T Stadium",      City = "Arlington"         }, // 12 PM UTC-5
        new() { Id = "58", MatchNumber = 58, GroupLetter = "J", HomeTeamId = "JOR", AwayTeamId = "ALG", KickoffUtc = Utc(2026, 6, 22,  3,  0), Venue = "Levi's Stadium",    City = "Santa Clara"       }, // 8 PM UTC-7 Jun 21
        new() { Id = "59", MatchNumber = 59, GroupLetter = "J", HomeTeamId = "ALG", AwayTeamId = "AUT", KickoffUtc = Utc(2026, 6, 27,  2,  0), Venue = "Arrowhead Stadium", City = "Kansas City"       }, // 9 PM UTC-5 Jun 26
        new() { Id = "60", MatchNumber = 60, GroupLetter = "J", HomeTeamId = "JOR", AwayTeamId = "ARG", KickoffUtc = Utc(2026, 6, 27,  2,  0), Venue = "AT&T Stadium",      City = "Arlington"         }, // 9 PM UTC-5 Jun 26

        // ═══════════════════════════════════════════════════════════════════
        // GROUP K  — POR, COD, UZB, COL
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "61", MatchNumber = 61, GroupLetter = "K", HomeTeamId = "POR", AwayTeamId = "COD", KickoffUtc = Utc(2026, 6, 17, 17,  0), Venue = "NRG Stadium",       City = "Houston"           }, // 12 PM UTC-5
        new() { Id = "62", MatchNumber = 62, GroupLetter = "K", HomeTeamId = "UZB", AwayTeamId = "COL", KickoffUtc = Utc(2026, 6, 18,  2,  0), Venue = "Estadio Azteca",    City = "Mexico City"       }, // 8 PM UTC-6 Jun 17
        new() { Id = "63", MatchNumber = 63, GroupLetter = "K", HomeTeamId = "POR", AwayTeamId = "UZB", KickoffUtc = Utc(2026, 6, 23, 17,  0), Venue = "NRG Stadium",       City = "Houston"           },
        new() { Id = "64", MatchNumber = 64, GroupLetter = "K", HomeTeamId = "COL", AwayTeamId = "COD", KickoffUtc = Utc(2026, 6, 24,  2,  0), Venue = "Estadio Akron",     City = "Zapopan"           }, // 8 PM UTC-6 Jun 23
        new() { Id = "65", MatchNumber = 65, GroupLetter = "K", HomeTeamId = "COL", AwayTeamId = "POR", KickoffUtc = Utc(2026, 6, 27, 23, 30), Venue = "Hard Rock Stadium", City = "Miami Gardens"     },
        new() { Id = "66", MatchNumber = 66, GroupLetter = "K", HomeTeamId = "COD", AwayTeamId = "UZB", KickoffUtc = Utc(2026, 6, 27, 23, 30), Venue = "Mercedes-Benz Stadium", City = "Atlanta"       },

        // ═══════════════════════════════════════════════════════════════════
        // GROUP L  — ENG, CRO, GHA, PAN
        // ═══════════════════════════════════════════════════════════════════
        new() { Id = "67", MatchNumber = 67, GroupLetter = "L", HomeTeamId = "ENG", AwayTeamId = "CRO", KickoffUtc = Utc(2026, 6, 17, 19,  0), Venue = "AT&T Stadium",      City = "Arlington"         }, // 2 PM UTC-5
        new() { Id = "68", MatchNumber = 68, GroupLetter = "L", HomeTeamId = "GHA", AwayTeamId = "PAN", KickoffUtc = Utc(2026, 6, 17, 23,  0), Venue = "BMO Field",         City = "Toronto"           }, // 7 PM UTC-4
        new() { Id = "69", MatchNumber = 69, GroupLetter = "L", HomeTeamId = "ENG", AwayTeamId = "GHA", KickoffUtc = Utc(2026, 6, 23, 20,  0), Venue = "Gillette Stadium",  City = "Foxborough"        }, // 4 PM UTC-4
        new() { Id = "70", MatchNumber = 70, GroupLetter = "L", HomeTeamId = "PAN", AwayTeamId = "CRO", KickoffUtc = Utc(2026, 6, 23, 23,  0), Venue = "BMO Field",         City = "Toronto"           }, // 7 PM UTC-4
        new() { Id = "71", MatchNumber = 71, GroupLetter = "L", HomeTeamId = "PAN", AwayTeamId = "ENG", KickoffUtc = Utc(2026, 6, 27, 21,  0), Venue = "MetLife Stadium",   City = "East Rutherford"   },
        new() { Id = "72", MatchNumber = 72, GroupLetter = "L", HomeTeamId = "CRO", AwayTeamId = "GHA", KickoffUtc = Utc(2026, 6, 27, 21,  0), Venue = "Lincoln Financial Field", City = "Philadelphia" },
    ];
}
