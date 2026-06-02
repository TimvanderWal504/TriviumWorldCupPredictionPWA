using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Data.SeedData;

/// <summary>
/// 12 groups (A–L) with confirmed team assignments from the December 2024 draw.
/// Source: https://en.wikipedia.org/wiki/2026_FIFA_World_Cup
/// </summary>
public static class GroupsData
{
    public static IReadOnlyList<Group> All => _all;

    private static readonly List<Group> _all =
    [
        new() { Id = "A", Letter = "A", TeamIds = ["MEX", "RSA", "KOR", "CZE"] },
        new() { Id = "B", Letter = "B", TeamIds = ["CAN", "BIH", "QAT", "SUI"] },
        new() { Id = "C", Letter = "C", TeamIds = ["BRA", "MAR", "HTI", "SCO"] },
        new() { Id = "D", Letter = "D", TeamIds = ["USA", "PAR", "AUS", "TUR"] },
        new() { Id = "E", Letter = "E", TeamIds = ["GER", "CUW", "CIV", "ECU"] },
        new() { Id = "F", Letter = "F", TeamIds = ["NED", "JPN", "SWE", "TUN"] },
        new() { Id = "G", Letter = "G", TeamIds = ["BEL", "EGY", "IRN", "NZL"] },
        new() { Id = "H", Letter = "H", TeamIds = ["ESP", "CPV", "KSA", "URU"] },
        new() { Id = "I", Letter = "I", TeamIds = ["FRA", "SEN", "IRQ", "NOR"] },
        new() { Id = "J", Letter = "J", TeamIds = ["ARG", "ALG", "AUT", "JOR"] },
        new() { Id = "K", Letter = "K", TeamIds = ["POR", "COD", "UZB", "COL"] },
        new() { Id = "L", Letter = "L", TeamIds = ["ENG", "CRO", "GHA", "PAN"] },
    ];
}
