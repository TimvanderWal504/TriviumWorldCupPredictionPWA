namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A goal scored by a specific player in a specific fixture.
/// TWC-9 ingests these; TWC-8 defines the structure for the scoring engine to query.
/// Marten identity: Id (Guid).
/// </summary>
public class GoalEvent
{
    public Guid Id { get; set; }

    /// <summary>Matches Fixture.Id (the group-stage fixture's string id).</summary>
    public string FixtureId { get; set; } = default!;

    /// <summary>The player who scored (matches Player.Id).</summary>
    public Guid PlayerId { get; set; }

    public GoalType Type { get; set; }

    /// <summary>Minute in which the goal was scored.</summary>
    public int Minute { get; set; }
}

public enum GoalType
{
    OpenPlay,
    PenaltyInMatch,  // in-match penalty — counts for Golden Six
    Shootout,        // penalty shootout — does NOT count
    OwnGoal          // does NOT count
}
