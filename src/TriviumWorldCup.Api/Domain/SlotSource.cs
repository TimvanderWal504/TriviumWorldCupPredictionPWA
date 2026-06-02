namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Describes where a team in a knockout slot comes from.
/// Examples:
///   "Winner of Group A"  → Type=GroupWinner, Reference="A"
///   "Runner-up of Group B" → Type=GroupRunnerUp, Reference="B"
///   "Best third place from groups C/D/E/F" → Type=BestThirdPlace, Reference="C/D/E/F"
///   "Winner of R32 slot 7" → Type=MatchWinner, Reference="R32-7"
///   "Loser of SF slot 1"   → Type=MatchLoser,  Reference="SF-1"
/// </summary>
public class SlotSource
{
    public SlotSourceType Type { get; set; }

    /// <summary>
    /// Free-form reference string. For group sources: group letter(s).
    /// For match sources: "{Round}-{SlotNumber}", e.g. "R32-7".
    /// </summary>
    public string Reference { get; set; } = default!;

    public override string ToString() => $"{Type}:{Reference}";
}
