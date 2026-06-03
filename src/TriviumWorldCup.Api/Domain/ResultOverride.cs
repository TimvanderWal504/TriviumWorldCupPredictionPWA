namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Audit record for a manual result or goal-event override performed by an admin.
/// Marten identity: Id (Guid).
/// </summary>
public class ResultOverride
{
    public Guid Id { get; set; }

    /// <summary>UserId of the admin who performed the override.</summary>
    public string AdminUserId { get; set; } = default!;

    /// <summary>Display name of the admin at the time of the override.</summary>
    public string AdminDisplayName { get; set; } = default!;

    /// <summary>When the override was applied (UTC).</summary>
    public DateTimeOffset OverriddenAt { get; set; }

    /// <summary>"fixture" or "goalevent".</summary>
    public string TargetType { get; set; } = default!;

    /// <summary>The string or Guid id of the affected document.</summary>
    public string TargetId { get; set; } = default!;

    /// <summary>Human-readable summary of what changed (e.g. "Set result 2-1").</summary>
    public string Description { get; set; } = default!;
}
