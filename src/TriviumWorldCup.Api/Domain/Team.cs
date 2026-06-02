namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Represents a national team competing in the tournament.
/// Marten identity: FifaCode (3-letter code, e.g. "BRA").
/// </summary>
public class Team
{
    /// <summary>Marten document id — same as FifaCode for ease of lookup.</summary>
    public string Id { get; set; } = default!;

    /// <summary>3-letter FIFA country code, e.g. "BRA", "USA".</summary>
    public string FifaCode { get; set; } = default!;

    /// <summary>Full country name, e.g. "Brazil".</summary>
    public string Name { get; set; } = default!;

    /// <summary>ISO 3166-1 alpha-2 country code for flag rendering, e.g. "BR".</summary>
    public string CountryCode { get; set; } = default!;

    /// <summary>Group letter the team is placed in (A–L).</summary>
    public string GroupLetter { get; set; } = default!;
}
