namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Represents one of the 12 groups (A–L) in the FIFA World Cup 2026.
/// Marten identity: Letter (single uppercase letter).
/// </summary>
public class Group
{
    /// <summary>Marten document id — same as Letter.</summary>
    public string Id { get; set; } = default!;

    /// <summary>Single uppercase letter, e.g. "A".</summary>
    public string Letter { get; set; } = default!;

    /// <summary>FifaCodes of the 4 teams in this group.</summary>
    public List<string> TeamIds { get; set; } = [];
}
