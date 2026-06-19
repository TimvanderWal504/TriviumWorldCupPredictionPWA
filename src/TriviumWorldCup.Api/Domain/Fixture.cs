namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A group-stage match between two known teams.
/// Marten identity: MatchNumber (1-based, 1–72 for group stage).
/// </summary>
public class Fixture
{
    /// <summary>Marten document id — same as MatchNumber (as string).</summary>
    public string Id { get; set; } = default!;

    /// <summary>Slug of the tournament this fixture belongs to (e.g. "world-cup-2026").</summary>
    public string TournamentId { get; set; } = SingleTournamentContext.DefaultTournamentId;

    /// <summary>Sequential match number within the tournament (1–72 for group stage).</summary>
    public int MatchNumber { get; set; }

    /// <summary>Group letter (A–L).</summary>
    public string GroupLetter { get; set; } = default!;

    /// <summary>Home team FIFA code.</summary>
    public string HomeTeamId { get; set; } = default!;

    /// <summary>Away team FIFA code.</summary>
    public string AwayTeamId { get; set; } = default!;

    /// <summary>Kickoff date and time in UTC.</summary>
    public DateTimeOffset KickoffUtc { get; set; }

    /// <summary>Venue / stadium name.</summary>
    public string Venue { get; set; } = default!;

    /// <summary>City where the match is played.</summary>
    public string City { get; set; } = default!;

    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;

    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    /// <summary>Clock minute from the API's status.elapsed (null when not live).</summary>
    public int? ElapsedMinute { get; set; }
    /// <summary>Stoppage-time extra minutes, e.g. 2 for "45+2'" (null when not in stoppage).</summary>
    public int? ElapsedExtra { get; set; }

    /// <summary>
    /// Integer fixture ID from the API-Football v3 API (fixtures.fixture.id).
    /// Populated on first contact via POST /admin/fixtures/sync-api-ids or automatically
    /// during the first live/completed ingestion cycle for this fixture.
    /// Null until then — use it to deep-link into API-Football data.
    /// </summary>
    public int? FootballApiFixtureId { get; set; }

    /// <summary>
    /// Track whether match events (goals, cards, substitutions) have been successfully
    /// fetched from the API. Set to true only after GetAllEventsAsync succeeds, even if
    /// zero events were returned.
    ///
    /// Decouples "score recorded" (Status == Completed) from "events recorded" so that:
    /// - If events fetch fails with 429 or timeout, the match stays Completed but EventsIngested=false
    /// - On the next poll after quota reset (or if quota improves), events backfill is attempted
    /// - Prevents losing events for a match forever due to a single transient failure
    ///
    /// Default: false. Set to true in ResultIngestionJob after a successful GetAllEventsAsync call.
    /// </summary>
    public bool EventsIngested { get; set; } = false;
}
