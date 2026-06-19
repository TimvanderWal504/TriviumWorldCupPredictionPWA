using System.ComponentModel;

namespace TriviumWorldCup.Api.Scheduling;

/// <summary>
/// Config-driven scheduling and live-window values (GEN-10 / TWC-44).
/// Bound from the <c>Scheduling</c> section of <c>appsettings.json</c>.
/// Named <c>TriviumSchedulingOptions</c> to avoid ambiguity with <c>Quartz.SchedulingOptions</c>.
/// </summary>
public class TriviumSchedulingOptions
{
    /// <summary>How often the result ingestion job polls the football API, in seconds. Default: 30.</summary>
    [DefaultValue(30)]
    public int PollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Width (in minutes) of the "live window" on each side of a fixture kickoff.
    /// Default: 30 minutes.
    /// </summary>
    [DefaultValue(30)]
    public int LiveWindowMinutes { get; set; } = 30;

    /// <summary>How many hours ahead the push-reminder job looks for upcoming fixtures. Default: 2.</summary>
    [DefaultValue(2)]
    public double PushReminderLookaheadHours { get; set; } = 2.0;

    /// <summary>How often the push reminder Quartz job fires, in minutes. Default: 30.</summary>
    [DefaultValue(30)]
    public int PushReminderIntervalMinutes { get; set; } = 30;
}
