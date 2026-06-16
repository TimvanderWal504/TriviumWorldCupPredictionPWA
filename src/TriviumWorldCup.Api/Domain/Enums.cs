namespace TriviumWorldCup.Api.Domain;

public enum Position
{
    GK,
    DEF,
    MID,
    FWD
}

public enum Round
{
    R32,
    R16,
    QF,
    SF,
    ThirdPlace,
    Final
}

public enum MatchStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled,
    ExtraTime,        // Live: match is in extra time (ET / break before penalties)
    PenaltyShootout,  // Live: penalty shootout in progress
    Postponed,        // Delayed by the API to an as-yet-unknown new kickoff time
}

public enum SlotSourceType
{
    GroupWinner,
    GroupRunnerUp,
    BestThirdPlace,
    MatchWinner,
    MatchLoser // only used for 3rd-place play-off
}
