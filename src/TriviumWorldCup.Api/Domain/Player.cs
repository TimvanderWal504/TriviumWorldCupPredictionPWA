namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A player in the tournament squad.
/// Marten identity: Id (Guid).
/// </summary>
public class Player
{
    /// <summary>Marten document id.</summary>
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>FIFA code of the player's national team.</summary>
    public string TeamId { get; set; } = default!;

    public Position Position { get; set; }

    /// <summary>Shirt number (optional — not always confirmed pre-tournament).</summary>
    public int? ShirtNumber { get; set; }
}
