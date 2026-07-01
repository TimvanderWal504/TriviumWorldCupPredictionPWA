namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Persisted scoring summary per member — fast-read store for standings (TWC-10) and leaderboard (TWC-11).
/// Recomputed idempotently by ScoringRecomputeService.
/// Marten identity: Id = UserId.
/// </summary>
public class MemberScore
{
    /// <summary>Marten document id — same as UserId.</summary>
    public string Id { get; set; } = default!;

    public string UserId { get; set; } = default!;

    /// <summary>Points accumulated from group-stage match predictions.</summary>
    public int GroupMatchPoints { get; set; }

    /// <summary>Points from the champion prediction (100 if correct, 0 otherwise).</summary>
    public int ChampionPoints { get; set; }

    /// <summary>Points from Golden Six player goal scoring.</summary>
    public int GoldenSixPoints { get; set; }

    /// <summary>Points accumulated from knockout-stage match predictions.</summary>
    public int KnockoutPoints { get; set; }

    /// <summary>Sum of all point categories.</summary>
    public int TotalPoints => GroupMatchPoints + ChampionPoints + GoldenSixPoints + KnockoutPoints;

    /// <summary>Count of exact scorelines predicted (tiebreaker 1, TWC-11).</summary>
    public int ExactScorelineCount { get; set; }

    /// <summary>Count of predictions that earned any tier >= 3 pts (tiebreaker 2, TWC-11).</summary>
    public int CorrectOutcomeCount { get; set; }

    /// <summary>Per-prediction breakdown for group-stage fixtures, keyed by FixtureId.</summary>
    public List<GroupPredictionScore> GroupBreakdown { get; set; } = [];

    /// <summary>Per-prediction breakdown for knockout slots, keyed by SlotKey.</summary>
    public List<KnockoutPredictionScore> KnockoutBreakdown { get; set; } = [];

    /// <summary>Per-player breakdown for the Golden Six picks.</summary>
    public List<GoldenSixPlayerScore> GoldenSixBreakdown { get; set; } = [];

    /// <summary>Timestamp of the last recompute run.</summary>
    public DateTimeOffset LastComputedAt { get; set; }
}

/// <summary>Points for one user's group-stage prediction, keyed by FixtureId.</summary>
public record GroupPredictionScore(string FixtureId, int Points, bool IsExact, bool IsCorrectOutcome);

/// <summary>
/// Points for one user's knockout prediction, keyed by SlotKey.
/// StreakMultiplier is 0 when the predicted winner was wrong (AdvancingPoints will also be 0).
/// </summary>
public record KnockoutPredictionScore(string SlotKey, int ScorePoints, int AdvancingPoints, int StreakMultiplier);

/// <summary>Points for one of the user's six picked Golden Six players.</summary>
public record GoldenSixPlayerScore(Guid PlayerId, int Goals, int Points);
