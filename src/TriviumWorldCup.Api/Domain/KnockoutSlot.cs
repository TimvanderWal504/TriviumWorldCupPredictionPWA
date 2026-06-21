namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A single match slot in the knockout bracket.
/// Teams are null until the preceding round resolves.
/// Marten identity: SlotKey (e.g. "R32-1", "R16-1", "QF-1", "SF-1", "3RD", "FIN").
/// </summary>
public class KnockoutSlot
{
    /// <summary>Marten document id — same as SlotKey.</summary>
    public string Id { get; set; } = default!;

    /// <summary>Slug of the tournament this slot belongs to (e.g. "world-cup-2026").</summary>
    public string TournamentId { get; set; } = SingleTournamentContext.DefaultTournamentId;

    /// <summary>Unique key, e.g. "R32-1", "R16-4", "QF-2", "SF-1", "3RD", "FIN".</summary>
    public string SlotKey { get; set; } = default!;

    public Round Round { get; set; }

    /// <summary>1-based position within the round (e.g. 1–16 for R32).</summary>
    public int SlotNumber { get; set; }

    /// <summary>Describes which team fills the home position.</summary>
    public SlotSource HomeSlotSource { get; set; } = default!;

    /// <summary>Describes which team fills the away position.</summary>
    public SlotSource AwaySlotSource { get; set; } = default!;

    /// <summary>Resolved home team FIFA code — null until the source match completes.</summary>
    public string? HomeTeamId { get; set; }

    /// <summary>Resolved away team FIFA code — null until the source match completes.</summary>
    public string? AwayTeamId { get; set; }

    public DateTimeOffset? KickoffUtc { get; set; }

    public string? Venue { get; set; }

    public string? City { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;

    /// <summary>90-minute score — used by the prediction scoring system.</summary>
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    /// <summary>
    /// Penalty shootout scores — set when the match was decided by penalties (StatusShort "PEN").
    /// Null for matches decided in 90 min or extra time.
    /// </summary>
    public int? PenaltyHomeScore { get; set; }
    public int? PenaltyAwayScore { get; set; }

    /// <summary>
    /// FIFA code of the match winner — populated by TWC-9 when the slot result is recorded.
    /// Used by ScoringRecomputeService to determine the tournament champion from the Final slot.
    /// </summary>
    public string? WinnerTeamId { get; set; }

    /// <summary>
    /// Integer fixture ID from the API-Football v3 API (fixtures.fixture.id).
    /// Null until the slot's teams are resolved and the ingestion job matches it by team pair.
    /// Once populated, subsequent ingestion cycles match by this ID instead of the team pair,
    /// which is more reliable if team-name resolution is ambiguous.
    /// </summary>
    public int? FootballApiFixtureId { get; set; }
}
