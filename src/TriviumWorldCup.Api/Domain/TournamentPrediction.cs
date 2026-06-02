namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A member's tournament-level prediction: champion team + Golden Six top scorers.
/// Marten identity: Id = UserId (one document per member).
/// </summary>
public class TournamentPrediction
{
    /// <summary>Marten document id — same as UserId.</summary>
    public string Id { get; set; } = default!;

    /// <summary>The auth UserId who owns this prediction.</summary>
    public string UserId { get; set; } = default!;

    /// <summary>FIFA code of the team predicted to win the tournament (nullable until saved).</summary>
    public string? ChampionTeamId { get; set; }

    /// <summary>Exactly six player IDs predicted as the Golden Six top scorers.</summary>
    public List<Guid> GoldenSixPlayerIds { get; set; } = new();

    /// <summary>UTC timestamp of the last submission.</summary>
    public DateTimeOffset SubmittedAt { get; set; }
}
