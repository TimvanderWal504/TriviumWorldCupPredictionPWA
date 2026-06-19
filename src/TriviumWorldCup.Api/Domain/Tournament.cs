namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// GEN-1 (TWC-35): Root aggregate that represents a single tournament.
///
/// A single deployment hosts exactly one active tournament, identified by
/// <see cref="ITournamentContext.TournamentId"/>. The <see cref="Id"/> and
/// <see cref="Slug"/> are the same slug-style string (e.g. "world-cup-2026").
///
/// Stub reference fields (<see cref="StructureRef"/>, etc.) are populated by
/// future stories GEN-2, GEN-6, GEN-7, GEN-8 once the referenced documents exist.
/// </summary>
public class Tournament
{
    /// <summary>Marten document identity — same as <see cref="Slug"/>.</summary>
    public string Id { get; set; } = default!;

    /// <summary>URL-safe slug, e.g. "world-cup-2026".</summary>
    public string Slug { get; set; } = default!;

    /// <summary>Human-readable name shown in the UI.</summary>
    public string DisplayName { get; set; } = default!;

    /// <summary>Sport domain, e.g. "football". Future stories use this to select providers.</summary>
    public string SportKey { get; set; } = default!;

    public TournamentStatus Status { get; set; } = TournamentStatus.Upcoming;

    public DateTimeOffset? StartUtc { get; set; }
    public DateTimeOffset? EndUtc   { get; set; }

    // ── Stub references for future stories ───────────────────────────────────
    /// <summary>GEN-2: reference to the StructureConfig document id.</summary>
    public string? StructureRef    { get; set; }
    /// <summary>GEN-6: reference to the ScoringConfig document id.</summary>
    public string? ScoringConfigRef { get; set; }
    /// <summary>GEN-7: reference to the LockPolicy document id.</summary>
    public string? LockPolicyRef   { get; set; }
    /// <summary>GEN-8: reference to the ProviderConfig document id.</summary>
    public string? ProviderConfigRef { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public enum TournamentStatus
{
    Upcoming,
    Active,
    Completed,
    Cancelled,
}
