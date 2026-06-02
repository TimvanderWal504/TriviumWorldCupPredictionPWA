using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// 12 groups (A–L), each containing the FIFA codes of the 4 teams in that group.
/// Derived from TeamsData.All — kept in sync automatically by the seed logic.
/// This file provides an explicit list for clarity; the seed also builds groups
/// dynamically from team GroupLetter assignments.
/// </summary>
public static class GroupsData
{
    public static IReadOnlyList<Group> All => _all;

    private static readonly List<Group> _all =
    [
        new() { Id = "A", Letter = "A", TeamIds = ["USA", "URU", "PAN", "BOL"] },
        new() { Id = "B", Letter = "B", TeamIds = ["MEX", "ARG", "NZL", "JAM"] },
        new() { Id = "C", Letter = "C", TeamIds = ["CAN", "MAR", "ECU", "TUN"] },
        new() { Id = "D", Letter = "D", TeamIds = ["BRA", "GER", "JPN", "CMR"] },
        new() { Id = "E", Letter = "E", TeamIds = ["ESP", "SRB", "COL", "NGR"] },
        new() { Id = "F", Letter = "F", TeamIds = ["POR", "CRO", "SEN", "IRI"] },
        new() { Id = "G", Letter = "G", TeamIds = ["ENG", "FRA", "AUS", "VEN"] },
        new() { Id = "H", Letter = "H", TeamIds = ["NED", "NOR", "CIV", "CHI"] },
        new() { Id = "I", Letter = "I", TeamIds = ["BEL", "TUR", "GHA", "KSA"] },
        new() { Id = "J", Letter = "J", TeamIds = ["KOR", "AUT", "QAT", "ALG"] },
        new() { Id = "K", Letter = "K", TeamIds = ["POL", "SUI", "RSA", "EGY"] },
        new() { Id = "L", Letter = "L", TeamIds = ["PER", "UZB", "IRQ", "PAR"] },
    ];
}
