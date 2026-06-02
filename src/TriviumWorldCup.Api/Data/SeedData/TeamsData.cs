using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// 48 teams across 12 groups (A–L) from the confirmed FIFA World Cup 2026 draw
/// held at Kaseya Center, Miami on 4 December 2024.
/// Source: https://en.wikipedia.org/wiki/2026_FIFA_World_Cup (verified June 2026)
/// </summary>
public static class TeamsData
{
    public static IReadOnlyList<Team> All => _all;

    private static readonly List<Team> _all =
    [
        // ── Group A ─────────────────────────────────────────────────────────
        new() { Id = "MEX", FifaCode = "MEX", Name = "Mexico",              CountryCode = "MX",     GroupLetter = "A" },
        new() { Id = "RSA", FifaCode = "RSA", Name = "South Africa",        CountryCode = "ZA",     GroupLetter = "A" },
        new() { Id = "KOR", FifaCode = "KOR", Name = "South Korea",         CountryCode = "KR",     GroupLetter = "A" },
        new() { Id = "CZE", FifaCode = "CZE", Name = "Czech Republic",      CountryCode = "CZ",     GroupLetter = "A" },

        // ── Group B ─────────────────────────────────────────────────────────
        new() { Id = "CAN", FifaCode = "CAN", Name = "Canada",              CountryCode = "CA",     GroupLetter = "B" },
        new() { Id = "BIH", FifaCode = "BIH", Name = "Bosnia & Herzegovina",CountryCode = "BA",     GroupLetter = "B" },
        new() { Id = "QAT", FifaCode = "QAT", Name = "Qatar",               CountryCode = "QA",     GroupLetter = "B" },
        new() { Id = "SUI", FifaCode = "SUI", Name = "Switzerland",         CountryCode = "CH",     GroupLetter = "B" },

        // ── Group C ─────────────────────────────────────────────────────────
        new() { Id = "BRA", FifaCode = "BRA", Name = "Brazil",              CountryCode = "BR",     GroupLetter = "C" },
        new() { Id = "MAR", FifaCode = "MAR", Name = "Morocco",             CountryCode = "MA",     GroupLetter = "C" },
        new() { Id = "HTI", FifaCode = "HTI", Name = "Haiti",               CountryCode = "HT",     GroupLetter = "C" },
        new() { Id = "SCO", FifaCode = "SCO", Name = "Scotland",            CountryCode = "GB-SCT", GroupLetter = "C" },

        // ── Group D ─────────────────────────────────────────────────────────
        new() { Id = "USA", FifaCode = "USA", Name = "United States",       CountryCode = "US",     GroupLetter = "D" },
        new() { Id = "PAR", FifaCode = "PAR", Name = "Paraguay",            CountryCode = "PY",     GroupLetter = "D" },
        new() { Id = "AUS", FifaCode = "AUS", Name = "Australia",           CountryCode = "AU",     GroupLetter = "D" },
        new() { Id = "TUR", FifaCode = "TUR", Name = "Turkey",              CountryCode = "TR",     GroupLetter = "D" },

        // ── Group E ─────────────────────────────────────────────────────────
        new() { Id = "GER", FifaCode = "GER", Name = "Germany",             CountryCode = "DE",     GroupLetter = "E" },
        new() { Id = "CUW", FifaCode = "CUW", Name = "Curaçao",             CountryCode = "CW",     GroupLetter = "E" },
        new() { Id = "CIV", FifaCode = "CIV", Name = "Ivory Coast",         CountryCode = "CI",     GroupLetter = "E" },
        new() { Id = "ECU", FifaCode = "ECU", Name = "Ecuador",             CountryCode = "EC",     GroupLetter = "E" },

        // ── Group F ─────────────────────────────────────────────────────────
        new() { Id = "NED", FifaCode = "NED", Name = "Netherlands",         CountryCode = "NL",     GroupLetter = "F" },
        new() { Id = "JPN", FifaCode = "JPN", Name = "Japan",               CountryCode = "JP",     GroupLetter = "F" },
        new() { Id = "SWE", FifaCode = "SWE", Name = "Sweden",              CountryCode = "SE",     GroupLetter = "F" },
        new() { Id = "TUN", FifaCode = "TUN", Name = "Tunisia",             CountryCode = "TN",     GroupLetter = "F" },

        // ── Group G ─────────────────────────────────────────────────────────
        new() { Id = "BEL", FifaCode = "BEL", Name = "Belgium",             CountryCode = "BE",     GroupLetter = "G" },
        new() { Id = "EGY", FifaCode = "EGY", Name = "Egypt",               CountryCode = "EG",     GroupLetter = "G" },
        new() { Id = "IRN", FifaCode = "IRN", Name = "Iran",                CountryCode = "IR",     GroupLetter = "G" },
        new() { Id = "NZL", FifaCode = "NZL", Name = "New Zealand",         CountryCode = "NZ",     GroupLetter = "G" },

        // ── Group H ─────────────────────────────────────────────────────────
        new() { Id = "ESP", FifaCode = "ESP", Name = "Spain",               CountryCode = "ES",     GroupLetter = "H" },
        new() { Id = "CPV", FifaCode = "CPV", Name = "Cape Verde",          CountryCode = "CV",     GroupLetter = "H" },
        new() { Id = "KSA", FifaCode = "KSA", Name = "Saudi Arabia",        CountryCode = "SA",     GroupLetter = "H" },
        new() { Id = "URU", FifaCode = "URU", Name = "Uruguay",             CountryCode = "UY",     GroupLetter = "H" },

        // ── Group I ─────────────────────────────────────────────────────────
        new() { Id = "FRA", FifaCode = "FRA", Name = "France",              CountryCode = "FR",     GroupLetter = "I" },
        new() { Id = "SEN", FifaCode = "SEN", Name = "Senegal",             CountryCode = "SN",     GroupLetter = "I" },
        new() { Id = "IRQ", FifaCode = "IRQ", Name = "Iraq",                CountryCode = "IQ",     GroupLetter = "I" },
        new() { Id = "NOR", FifaCode = "NOR", Name = "Norway",              CountryCode = "NO",     GroupLetter = "I" },

        // ── Group J ─────────────────────────────────────────────────────────
        new() { Id = "ARG", FifaCode = "ARG", Name = "Argentina",           CountryCode = "AR",     GroupLetter = "J" },
        new() { Id = "ALG", FifaCode = "ALG", Name = "Algeria",             CountryCode = "DZ",     GroupLetter = "J" },
        new() { Id = "AUT", FifaCode = "AUT", Name = "Austria",             CountryCode = "AT",     GroupLetter = "J" },
        new() { Id = "JOR", FifaCode = "JOR", Name = "Jordan",              CountryCode = "JO",     GroupLetter = "J" },

        // ── Group K ─────────────────────────────────────────────────────────
        new() { Id = "POR", FifaCode = "POR", Name = "Portugal",            CountryCode = "PT",     GroupLetter = "K" },
        new() { Id = "COD", FifaCode = "COD", Name = "DR Congo",            CountryCode = "CD",     GroupLetter = "K" },
        new() { Id = "UZB", FifaCode = "UZB", Name = "Uzbekistan",          CountryCode = "UZ",     GroupLetter = "K" },
        new() { Id = "COL", FifaCode = "COL", Name = "Colombia",            CountryCode = "CO",     GroupLetter = "K" },

        // ── Group L ─────────────────────────────────────────────────────────
        new() { Id = "ENG", FifaCode = "ENG", Name = "England",             CountryCode = "GB-ENG", GroupLetter = "L" },
        new() { Id = "CRO", FifaCode = "CRO", Name = "Croatia",             CountryCode = "HR",     GroupLetter = "L" },
        new() { Id = "GHA", FifaCode = "GHA", Name = "Ghana",               CountryCode = "GH",     GroupLetter = "L" },
        new() { Id = "PAN", FifaCode = "PAN", Name = "Panama",              CountryCode = "PA",     GroupLetter = "L" },
    ];
}
