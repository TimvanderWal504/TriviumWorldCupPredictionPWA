namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A user's predicted winner and optional 90-minute score for a single knockout bracket slot.
/// Marten identity: "{UserId}_{SlotKey}" — one prediction per user per slot.
/// </summary>
public class KnockoutPrediction
{
    /// <summary>Composite key: "{TournamentId}_{UserId}_{SlotKey}".</summary>
    public string Id { get; set; } = default!;

    /// <summary>Slug of the tournament this prediction belongs to (e.g. "world-cup-2026").</summary>
    public string TournamentId { get; set; } = SingleTournamentContext.DefaultTournamentId;

    /// <summary>The user who submitted this prediction.</summary>
    public string UserId { get; set; } = default!;

    /// <summary>The bracket slot this prediction is for (matches KnockoutSlot.SlotKey).</summary>
    public string SlotKey { get; set; } = default!;

    /// <summary>
    /// FIFA code of the team predicted to advance.
    /// Must equal KnockoutSlot.HomeTeamId or KnockoutSlot.AwayTeamId at time of submission.
    /// </summary>
    public string PredictedWinnerTeamId { get; set; } = default!;

    /// <summary>Predicted 90-minute home score — optional.</summary>
    public int? PredictedHomeScore { get; set; }

    /// <summary>Predicted 90-minute away score — optional.</summary>
    public int? PredictedAwayScore { get; set; }

    /// <summary>When this prediction was last submitted/updated.</summary>
    public DateTimeOffset SubmittedAt { get; set; }
}
