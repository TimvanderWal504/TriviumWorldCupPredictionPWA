namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A user's predicted scoreline for a single group-stage fixture.
/// Marten identity: "{UserId}_{FixtureId}" — one prediction per user per fixture.
/// </summary>
public class GroupPrediction
{
    /// <summary>Composite key: "{UserId}_{FixtureId}".</summary>
    public string Id { get; set; } = default!;

    /// <summary>The user who submitted this prediction.</summary>
    public string UserId { get; set; } = default!;

    /// <summary>The fixture this prediction is for (matches Fixture.Id).</summary>
    public string FixtureId { get; set; } = default!;

    /// <summary>Predicted home team score.</summary>
    public int HomeScore { get; set; }

    /// <summary>Predicted away team score.</summary>
    public int AwayScore { get; set; }

    /// <summary>When this prediction was last submitted/updated.</summary>
    public DateTimeOffset SubmittedAt { get; set; }
}
