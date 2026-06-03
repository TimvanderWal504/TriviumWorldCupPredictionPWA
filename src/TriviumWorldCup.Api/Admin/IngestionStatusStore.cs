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
}
