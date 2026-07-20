namespace TriviumWorldCup.Api.Verification;

/// <summary>
/// Result of a points verification run. <see cref="Users"/> contains only members with
/// at least one discrepancy — a clean run returns an empty list.
/// </summary>
public record ScoreVerificationReport(
    DateTimeOffset VerifiedAt,
    int UsersChecked,
    int UsersWithDiscrepancies,
    int TotalDiscrepancies,
    IReadOnlyList<UserScoreVerification> Users);

/// <summary>
/// All discrepancies found for a single member.
/// <para><c>MissingScoreDocument</c> — the member has predictions but no MemberScore document.</para>
/// <para><c>OrphanedScoreDocument</c> — a MemberScore exists for a member with no predictions.</para>
/// </summary>
public record UserScoreVerification(
    string UserId,
    bool MissingScoreDocument,
    bool OrphanedScoreDocument,
    DateTimeOffset? LastComputedAt,
    IReadOnlyList<FieldDiscrepancy> Totals,
    IReadOnlyList<PredictionDiscrepancy> Predictions);

/// <summary>A stored aggregate that disagrees with the independently derived value.</summary>
public record FieldDiscrepancy(string Field, int Stored, int Expected)
{
    public int Delta => Expected - Stored;
}

/// <summary>
/// A single prediction whose stored per-prediction breakdown entry disagrees with the
/// independently derived value. <c>Kind</c> is "group", "knockout" or "goldensix".
/// </summary>
public record PredictionDiscrepancy(
    string Kind,
    string Key,
    string Detail,
    int Stored,
    int Expected)
{
    public int Delta => Expected - Stored;
}
