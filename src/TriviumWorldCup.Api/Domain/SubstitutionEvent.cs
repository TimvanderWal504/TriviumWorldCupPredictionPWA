namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A player substitution in a specific fixture.
/// Ingested by ResultIngestionJob; can also be added via admin endpoints.
/// Marten identity: Id (Guid).
/// </summary>
public class SubstitutionEvent
{
    public Guid Id { get; set; }

    /// <summary>Matches Fixture.Id.</summary>
    public string FixtureId { get; set; } = default!;

    /// <summary>Player entering the field. Null if name could not be matched to roster.</summary>
    public Guid? PlayerInId { get; set; }

    /// <summary>Player leaving the field. Null if name could not be matched to roster.</summary>
    public Guid? PlayerOutId { get; set; }

    /// <summary>Raw name from the API or admin, always populated for display fallback.</summary>
    public string PlayerInName { get; set; } = string.Empty;

    /// <summary>Raw name from the API or admin, always populated for display fallback.</summary>
    public string PlayerOutName { get; set; } = string.Empty;

    /// <summary>FIFA team code of the team making the substitution.</summary>
    public string TeamId { get; set; } = string.Empty;

    public int Minute { get; set; }

    /// <summary>Stoppage-time minutes added within the period (e.g. 2 for "45+2"). Null when not in stoppage time.</summary>
    public int? ExtraMinute { get; set; }
}
