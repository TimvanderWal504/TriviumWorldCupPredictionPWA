namespace TriviumWorldCup.Api.Scheduling;

/// <summary>
/// Configuration for scheduling options: live-window width and push-reminder lookahead.
/// Bound from the <c>Scheduling</c> configuration section.
/// </summary>
public class TriviumSchedulingOptions
{
    /// <summary>
    /// Width (in minutes) of the "live window" on each side of a fixture kickoff.
    /// The result ingestion job considers any fixture with kickoff within
    /// <c>now ± LiveWindowMinutes</c> as "live" and calls the Football API.
    /// Default: 30 minutes.
    /// </summary>
    public int LiveWindowMinutes { get; set; } = 30;

    /// <summary>
    /// How many hours ahead the push-reminder job looks for upcoming fixtures
    /// when deciding which subscribers to notify. Default: 2 hours.
    /// </summary>
    public double PushReminderLookaheadHours { get; set; } = 2.0;
}
