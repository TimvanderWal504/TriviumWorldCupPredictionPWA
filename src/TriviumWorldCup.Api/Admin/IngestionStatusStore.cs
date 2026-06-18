namespace TriviumWorldCup.Api.Admin;

/// <summary>
/// Singleton in-memory store updated by <see cref="TriviumWorldCup.Api.Ingestion.ResultIngestionJob"/>
/// after each poll cycle. Exposed via GET /admin/ingestion.
/// </summary>
public class IngestionStatusStore
{
    public DateTimeOffset? LastSuccessfulPoll { get; set; }
    public DateTimeOffset? LastAttemptedPoll { get; set; }
    public string? LastError { get; set; }
    public int TotalPollCount { get; set; }
    public int ErrorCount { get; set; }

    /// <summary>
    /// Last time the job called the Football API specifically to recheck Postponed
    /// fixtures/slots for a status change (new kickoff time, cancellation, or kickoff).
    /// Used to throttle that recheck to roughly once a minute regardless of poll interval.
    /// </summary>
    public DateTimeOffset? LastPostponedRecheck { get; set; }

    /// <summary>
    /// Last time <see cref="TriviumWorldCup.Api.Scoring.ScoringRecomputeService.RecomputeAllAsync"/> was
    /// actually invoked. Used by the job to enforce a minimum interval between recomputes so that
    /// simultaneous group-stage completions (which produce two ingestion cycles within 30 seconds)
    /// don't trigger back-to-back full recomputes unnecessarily.
    /// </summary>
    public DateTimeOffset? LastRecomputeUtc { get; set; }

    // ── Unmatched player events ───────────────────────────────────────────────
    // Tracks goal and card events skipped because the API player name could not be
    // resolved to a Player document. Deduplicated by (fixtureId, eventType, playerName)
    // so repeated live-poll cycles for the same player don't flood the list.
    private readonly object _unmatchedLock = new();
    private readonly HashSet<(string, string, string)> _unmatchedKeys = [];
    private readonly List<UnmatchedPlayerEvent> _unmatchedEvents = [];

    public IReadOnlyList<UnmatchedPlayerEvent> UnmatchedEvents
    {
        get { lock (_unmatchedLock) return [.. _unmatchedEvents]; }
    }

    public void RecordUnmatched(string fixtureId, string eventType, string playerName, int minute)
    {
        var key = (fixtureId, eventType, playerName);
        lock (_unmatchedLock)
        {
            if (_unmatchedKeys.Add(key))
                _unmatchedEvents.Add(new UnmatchedPlayerEvent(fixtureId, eventType, playerName, minute, DateTimeOffset.UtcNow));
        }
    }
}

/// <summary>A goal or card event the ingestion job could not attribute to a known player.</summary>
public sealed record UnmatchedPlayerEvent(
    string FixtureId,
    string EventType,
    string PlayerName,
    int Minute,
    DateTimeOffset SeenAt);
