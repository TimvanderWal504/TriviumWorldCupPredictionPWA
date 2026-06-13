namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A stored VAR decision for a fixture.
/// Only decision types that are meaningful to display are persisted:
/// goal cancellations and card upgrades. The logic effect (goal suppressed /
/// card type changed) is applied to GoalEvent/CardEvent; this document exists
/// solely so the UI can show the VAR timeline entry.
/// </summary>
public class VarEvent
{
    public Guid Id { get; set; }
    public string FixtureId { get; set; } = default!;
    public VarDecisionType Type { get; set; }

    /// <summary>Raw player name from the API (not resolved to a Player document).</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>FIFA team code.</summary>
    public string TeamId { get; set; } = string.Empty;

    public int Minute { get; set; }
    public int? ExtraMinute { get; set; }
}

public enum VarDecisionType
{
    GoalCancelled,
    CardUpgradeRed,
    CardUpgradeSecondYellow,
}
