namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Maps API-Football v3 team identifiers to the app's FIFA three-letter codes.
///
/// Strategy:
///   1. Name-based lookup (primary): API-Football returns the team name in every fixture response.
///      The name map covers all 48 World Cup 2026 participants with common alternate spellings.
///   2. ID-based lookup (supplementary): when the actual numeric team IDs are confirmed from the
///      API (call GET /teams?league=1&amp;season=2026), populate <see cref="AddKnownId"/> at startup
///      via configuration or a one-time discovery endpoint. The ID map starts empty.
///
/// The name-based lookup is always preferred as a fallback and is sufficient for correct operation.
/// ID-based resolution is a performance optimisation that avoids string comparison.
///
/// IMPORTANT: API-Football numeric team IDs are stable across seasons but cannot be reliably
/// hardcoded without calling the API. They are therefore not embedded here; only the name map
/// is hardcoded. The actual IDs would need to be fetched from the API and either stored in
/// configuration or discovered at startup.
///
/// If a team cannot be resolved by either method, <see cref="Resolve"/> returns null and the
/// fixture is skipped by the ingestion job (will be retried on next poll).
/// </summary>
public static class FootballApiTeamMap
{
    // Supplementary ID map — populated at runtime if API IDs are discovered.
    // Starts empty; add entries via AddKnownId() or populate from configuration.
    private static readonly Dictionary<int, string> _byId = new();

    // Name-based fallback map — normalised team name → FIFA code.
    // Case-insensitive. Covers all 48 WC 2026 teams plus common alternate names.
    private static readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Group A ─────────────────────────────────────────────────────────
        { "Mexico",                  "MEX" },
        { "South Africa",            "RSA" },
        { "South Korea",             "KOR" },
        { "Republic of Korea",       "KOR" },
        { "Korea Republic",          "KOR" },
        { "Czech Republic",          "CZE" },
        { "Czechia",                 "CZE" },

        // ── Group B ─────────────────────────────────────────────────────────
        { "Canada",                  "CAN" },
        { "Bosnia & Herzegovina",    "BIH" },
        { "Bosnia and Herzegovina",  "BIH" },
        { "Bosnia-Herzegovina",      "BIH" },
        { "Qatar",                   "QAT" },
        { "Switzerland",             "SUI" },

        // ── Group C ─────────────────────────────────────────────────────────
        { "Brazil",                  "BRA" },
        { "Morocco",                 "MAR" },
        { "Haiti",                   "HTI" },
        { "Scotland",                "SCO" },

        // ── Group D ─────────────────────────────────────────────────────────
        { "United States",           "USA" },
        { "USA",                     "USA" },
        { "US",                      "USA" },
        { "Paraguay",                "PAR" },
        { "Australia",               "AUS" },
        { "Turkey",                  "TUR" },
        { "Türkiye",                 "TUR" },

        // ── Group E ─────────────────────────────────────────────────────────
        { "Germany",                 "GER" },
        { "Curaçao",                 "CUW" },
        { "Curacao",                 "CUW" },
        { "Ivory Coast",             "CIV" },
        { "Côte d'Ivoire",           "CIV" },
        { "Cote d'Ivoire",           "CIV" },
        { "Ecuador",                 "ECU" },

        // ── Group F ─────────────────────────────────────────────────────────
        { "Netherlands",             "NED" },
        { "Japan",                   "JPN" },
        { "Sweden",                  "SWE" },
        { "Tunisia",                 "TUN" },

        // ── Group G ─────────────────────────────────────────────────────────
        { "Belgium",                 "BEL" },
        { "Egypt",                   "EGY" },
        { "Iran",                    "IRN" },
        { "IR Iran",                 "IRN" },
        { "New Zealand",             "NZL" },

        // ── Group H ─────────────────────────────────────────────────────────
        { "Spain",                   "ESP" },
        { "Cape Verde",              "CPV" },
        { "Cabo Verde",              "CPV" },
        { "Saudi Arabia",            "KSA" },
        { "Uruguay",                 "URU" },

        // ── Group I ─────────────────────────────────────────────────────────
        { "France",                  "FRA" },
        { "Senegal",                 "SEN" },
        { "Iraq",                    "IRQ" },
        { "Norway",                  "NOR" },

        // ── Group J ─────────────────────────────────────────────────────────
        { "Argentina",               "ARG" },
        { "Algeria",                 "ALG" },
        { "Austria",                 "AUT" },
        { "Jordan",                  "JOR" },

        // ── Group K ─────────────────────────────────────────────────────────
        { "Portugal",                "POR" },
        { "DR Congo",                "COD" },
        { "Congo DR",                "COD" },
        { "Democratic Republic of the Congo", "COD" },
        { "Uzbekistan",              "UZB" },
        { "Colombia",                "COL" },

        // ── Group L ─────────────────────────────────────────────────────────
        { "England",                 "ENG" },
        { "Croatia",                 "CRO" },
        { "Ghana",                   "GHA" },
        { "Panama",                  "PAN" },
    };

    /// <summary>
    /// Registers a known API-Football numeric team ID → FIFA code mapping at runtime.
    /// Call this once during startup after discovering IDs from the API.
    /// </summary>
    public static void AddKnownId(int apiTeamId, string fifaCode)
    {
        _byId[apiTeamId] = fifaCode;
    }

    /// <summary>
    /// Tries to resolve a FIFA code by numeric API-Football team ID.
    /// Returns false if the ID is not yet known (call AddKnownId or use name fallback).
    /// </summary>
    public static bool TryGetFifaCode(int apiTeamId, out string fifaCode)
    {
        return _byId.TryGetValue(apiTeamId, out fifaCode!);
    }

    /// <summary>
    /// Tries to map an API-Football team name to the app's FIFA three-letter code.
    /// Case-insensitive. Returns false if the name is not recognised.
    /// </summary>
    public static bool TryGetFifaCodeByName(string teamName, out string fifaCode)
    {
        return _byName.TryGetValue(teamName, out fifaCode!);
    }

    /// <summary>
    /// Resolves the FIFA code for a team, trying ID first then name.
    /// Returns null if neither lookup succeeds.
    /// </summary>
    public static string? Resolve(int apiTeamId, string teamName)
    {
        if (TryGetFifaCode(apiTeamId, out var byId))
            return byId;
        if (TryGetFifaCodeByName(teamName, out var byName))
            return byName;
        return null;
    }
}
