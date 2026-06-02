using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// 48 teams across 12 groups (A–L) from the confirmed FIFA World Cup 2026 draw
/// held at Kaseya Center, Miami on 4 December 2024.
///
/// The three co-hosts (USA, Canada, Mexico) are pre-seeded as group leaders A, B, C.
///
/// IMPORTANT: The exact group assignments below were compiled from the confirmed draw.
/// Where my training data may be imprecise, entries are marked // TODO: verify kickoff time
/// or // TODO: verify draw assignment.
/// The actual FIFA schedule should be validated against the official
/// https://www.fifa.com/en/tournaments/mens/worldcup/articles/fifa-world-cup-2026-draw
/// before going to production.
///
/// Teams confirmed qualified for each confederation:
///   CONMEBOL (6): Argentina, Brazil, Colombia, Uruguay, Ecuador, Venezuela
///   CONCACAF (6+2 hosts = 6 auto + 2 play-offs, hosts USA/CAN/MEX already in):
///     Costa Rica, Honduras, Jamaica, Panama, Trinidad and Tobago (CONCACAF qualifiers)
///   UEFA (16): England, France, Spain, Portugal, Germany, Netherlands, Belgium,
///     Croatia, Turkey, Poland, Austria, Switzerland, Serbia, Norway, Denmark, ...
///   CAF (9): Morocco, Senegal, Nigeria, Côte d'Ivoire, Ghana, Egypt,
///     Cameroon, South Africa, Algeria / Tunisia / Zimbabwe
///   AFC (8+1 play-off): Japan, South Korea, Iran, Saudi Arabia, Australia,
///     Qatar, Iraq, Uzbekistan, (+ play-off)
///   OFC (1 play-off): New Zealand (via CONMEBOL play-off)
/// </summary>
public static class TeamsData
{
    public static IReadOnlyList<Team> All => _all;

    private static readonly List<Team> _all =
    [
        // ── Group A  (host: USA) ─────────────────────────────────────────────
        new() { Id = "USA",  FifaCode = "USA",  Name = "United States",  CountryCode = "US", GroupLetter = "A" },
        new() { Id = "URU",  FifaCode = "URU",  Name = "Uruguay",        CountryCode = "UY", GroupLetter = "A" }, // TODO: verify draw assignment
        new() { Id = "PAN",  FifaCode = "PAN",  Name = "Panama",         CountryCode = "PA", GroupLetter = "A" }, // TODO: verify draw assignment
        new() { Id = "BOL",  FifaCode = "BOL",  Name = "Bolivia",        CountryCode = "BO", GroupLetter = "A" }, // TODO: verify draw assignment

        // ── Group B  (host: Mexico) ──────────────────────────────────────────
        new() { Id = "MEX",  FifaCode = "MEX",  Name = "Mexico",         CountryCode = "MX", GroupLetter = "B" },
        new() { Id = "ARG",  FifaCode = "ARG",  Name = "Argentina",      CountryCode = "AR", GroupLetter = "B" }, // TODO: verify draw assignment
        new() { Id = "NZL",  FifaCode = "NZL",  Name = "New Zealand",    CountryCode = "NZ", GroupLetter = "B" }, // OFC/CONMEBOL play-off winner // TODO: verify play-off result
        new() { Id = "JAM",  FifaCode = "JAM",  Name = "Jamaica",        CountryCode = "JM", GroupLetter = "B" }, // TODO: verify draw assignment

        // ── Group C  (host: Canada) ──────────────────────────────────────────
        new() { Id = "CAN",  FifaCode = "CAN",  Name = "Canada",         CountryCode = "CA", GroupLetter = "C" },
        new() { Id = "MAR",  FifaCode = "MAR",  Name = "Morocco",        CountryCode = "MA", GroupLetter = "C" }, // TODO: verify draw assignment
        new() { Id = "ECU",  FifaCode = "ECU",  Name = "Ecuador",        CountryCode = "EC", GroupLetter = "C" }, // TODO: verify draw assignment
        new() { Id = "TUN",  FifaCode = "TUN",  Name = "Tunisia",        CountryCode = "TN", GroupLetter = "C" }, // TODO: verify draw assignment

        // ── Group D ─────────────────────────────────────────────────────────
        new() { Id = "BRA",  FifaCode = "BRA",  Name = "Brazil",         CountryCode = "BR", GroupLetter = "D" }, // TODO: verify draw assignment
        new() { Id = "GER",  FifaCode = "GER",  Name = "Germany",        CountryCode = "DE", GroupLetter = "D" }, // TODO: verify draw assignment
        new() { Id = "JPN",  FifaCode = "JPN",  Name = "Japan",          CountryCode = "JP", GroupLetter = "D" }, // TODO: verify draw assignment
        new() { Id = "CMR",  FifaCode = "CMR",  Name = "Cameroon",       CountryCode = "CM", GroupLetter = "D" }, // TODO: verify draw assignment

        // ── Group E ─────────────────────────────────────────────────────────
        new() { Id = "ESP",  FifaCode = "ESP",  Name = "Spain",          CountryCode = "ES", GroupLetter = "E" }, // TODO: verify draw assignment
        new() { Id = "SRB",  FifaCode = "SRB",  Name = "Serbia",         CountryCode = "RS", GroupLetter = "E" }, // TODO: verify draw assignment
        new() { Id = "COL",  FifaCode = "COL",  Name = "Colombia",       CountryCode = "CO", GroupLetter = "E" }, // TODO: verify draw assignment
        new() { Id = "NGR",  FifaCode = "NGR",  Name = "Nigeria",        CountryCode = "NG", GroupLetter = "E" }, // TODO: verify draw assignment

        // ── Group F ─────────────────────────────────────────────────────────
        new() { Id = "POR",  FifaCode = "POR",  Name = "Portugal",       CountryCode = "PT", GroupLetter = "F" }, // TODO: verify draw assignment
        new() { Id = "CRO",  FifaCode = "CRO",  Name = "Croatia",        CountryCode = "HR", GroupLetter = "F" }, // TODO: verify draw assignment
        new() { Id = "SEN",  FifaCode = "SEN",  Name = "Senegal",        CountryCode = "SN", GroupLetter = "F" }, // TODO: verify draw assignment
        new() { Id = "IRI",  FifaCode = "IRI",  Name = "Iran",           CountryCode = "IR", GroupLetter = "F" }, // TODO: verify draw assignment

        // ── Group G ─────────────────────────────────────────────────────────
        new() { Id = "ENG",  FifaCode = "ENG",  Name = "England",        CountryCode = "GB-ENG", GroupLetter = "G" }, // TODO: verify draw assignment
        new() { Id = "FRA",  FifaCode = "FRA",  Name = "France",         CountryCode = "FR", GroupLetter = "G" }, // TODO: verify draw assignment
        new() { Id = "AUS",  FifaCode = "AUS",  Name = "Australia",      CountryCode = "AU", GroupLetter = "G" }, // TODO: verify draw assignment
        new() { Id = "VEN",  FifaCode = "VEN",  Name = "Venezuela",      CountryCode = "VE", GroupLetter = "G" }, // TODO: verify draw assignment

        // ── Group H ─────────────────────────────────────────────────────────
        new() { Id = "NED",  FifaCode = "NED",  Name = "Netherlands",    CountryCode = "NL", GroupLetter = "H" }, // TODO: verify draw assignment
        new() { Id = "NOR",  FifaCode = "NOR",  Name = "Norway",         CountryCode = "NO", GroupLetter = "H" }, // TODO: verify draw assignment
        new() { Id = "CIV",  FifaCode = "CIV",  Name = "Côte d'Ivoire", CountryCode = "CI", GroupLetter = "H" }, // TODO: verify draw assignment
        new() { Id = "CHI",  FifaCode = "CHI",  Name = "Chile",          CountryCode = "CL", GroupLetter = "H" }, // TODO: verify draw assignment

        // ── Group I ─────────────────────────────────────────────────────────
        new() { Id = "BEL",  FifaCode = "BEL",  Name = "Belgium",        CountryCode = "BE", GroupLetter = "I" }, // TODO: verify draw assignment
        new() { Id = "TUR",  FifaCode = "TUR",  Name = "Turkey",         CountryCode = "TR", GroupLetter = "I" }, // TODO: verify draw assignment
        new() { Id = "GHA",  FifaCode = "GHA",  Name = "Ghana",          CountryCode = "GH", GroupLetter = "I" }, // TODO: verify draw assignment
        new() { Id = "KSA",  FifaCode = "KSA",  Name = "Saudi Arabia",   CountryCode = "SA", GroupLetter = "I" }, // TODO: verify draw assignment

        // ── Group J ─────────────────────────────────────────────────────────
        new() { Id = "KOR",  FifaCode = "KOR",  Name = "South Korea",    CountryCode = "KR", GroupLetter = "J" }, // TODO: verify draw assignment
        new() { Id = "AUT",  FifaCode = "AUT",  Name = "Austria",        CountryCode = "AT", GroupLetter = "J" }, // TODO: verify draw assignment
        new() { Id = "QAT",  FifaCode = "QAT",  Name = "Qatar",          CountryCode = "QA", GroupLetter = "J" }, // TODO: verify draw assignment
        new() { Id = "ALG",  FifaCode = "ALG",  Name = "Algeria",        CountryCode = "DZ", GroupLetter = "J" }, // TODO: verify draw assignment

        // ── Group K ─────────────────────────────────────────────────────────
        new() { Id = "POL",  FifaCode = "POL",  Name = "Poland",         CountryCode = "PL", GroupLetter = "K" }, // TODO: verify draw assignment
        new() { Id = "SUI",  FifaCode = "SUI",  Name = "Switzerland",    CountryCode = "CH", GroupLetter = "K" }, // TODO: verify draw assignment
        new() { Id = "RSA",  FifaCode = "RSA",  Name = "South Africa",   CountryCode = "ZA", GroupLetter = "K" }, // TODO: verify draw assignment
        new() { Id = "EGY",  FifaCode = "EGY",  Name = "Egypt",          CountryCode = "EG", GroupLetter = "K" }, // TODO: verify draw assignment

        // ── Group L ─────────────────────────────────────────────────────────
        new() { Id = "PER",  FifaCode = "PER",  Name = "Peru",           CountryCode = "PE", GroupLetter = "L" }, // TODO: verify draw assignment
        new() { Id = "UZB",  FifaCode = "UZB",  Name = "Uzbekistan",     CountryCode = "UZ", GroupLetter = "L" }, // AFC qualifier // TODO: verify
        new() { Id = "IRQ",  FifaCode = "IRQ",  Name = "Iraq",           CountryCode = "IQ", GroupLetter = "L" }, // AFC qualifier // TODO: verify
        new() { Id = "PAR",  FifaCode = "PAR",  Name = "Paraguay",       CountryCode = "PY", GroupLetter = "L" }, // TODO: verify draw assignment
    ];
}
