namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A disciplinary card issued to a player in a specific fixture.
/// Ingested by ResultIngestionJob; can also be added/removed via admin endpoints.
/// Marten identity: Id (Guid).
/// </summary>
public class CardEvent
{
    public Guid Id { get; set; }

    /// <summary>Slug of the tournament this event belongs to (e.g. "world-cup-2026").</summary>
    public string TournamentId { get; set; } = SingleTournamentContext.DefaultTournamentId;

    /// <summary>Matches Fixture.Id (the group-stage fixture's string id).</summary>
    public string FixtureId { get; set; } = default!;

    /// <summary>The player who received the card (matches Player.Id).</summary>
    public Guid PlayerId { get; set; }

    public CardType Type { get; set; }

    /// <summary>Minute in which the card was issued.</summary>
    public int Minute { get; set; }

    /// <summary>Stoppage-time minutes added within the period (e.g. 2 for "45+2"). Null when not in stoppage time.</summary>
    public int? ExtraMinute { get; set; }
}

public enum CardType
{
    Yellow,
    SecondYellow, // second bookable offence — player is sent off
    Red           // straight red card
}
