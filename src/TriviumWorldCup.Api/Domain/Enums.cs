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
    Cancelled
}

public enum SlotSourceType
{
    GroupWinner,
    GroupRunnerUp,
    BestThirdPlace,
    MatchWinner,
    MatchLoser // only used for 3rd-place play-off
}
