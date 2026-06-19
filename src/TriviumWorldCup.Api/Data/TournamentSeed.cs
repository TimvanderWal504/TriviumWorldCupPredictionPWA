using Marten;
using TriviumWorldCup.Api.Data.SeedData;
using TriviumWorldCup.Api.Domain;
using TournamentAggregate = TriviumWorldCup.Api.Domain.Tournament;

namespace TriviumWorldCup.Api.Data;

/// <summary>
/// Idempotent seed for the tournament data.
/// Called from Program.cs at application startup.
///
/// Idempotency check: if any Team documents exist in the store, the seed is
/// considered already run and exits immediately. Run twice → no duplicates.
///
/// All documents use string-based Ids equal to their natural keys:
///   Team      → FifaCode   (e.g. "BRA")
///   Group     → Letter     (e.g. "A")
///   Fixture   → MatchNumber as string (e.g. "1" … "72")
///   KnockoutSlot → SlotKey (e.g. "R32-1", "FIN")
///   Player    → Guid (new Guid per player — not a natural key)
/// Marten upserts by Id so re-running after a partial seed is also safe.
/// </summary>
public static class TournamentSeed
{
    /// <summary>
    /// The default world-cup-2026 Tournament document.
    /// Exposed as a property so migration and unit tests can reference it.
    /// </summary>
    public static TournamentAggregate WorldCup2026 => new()
    {
        Id          = SingleTournamentContext.DefaultTournamentId,
        Slug        = SingleTournamentContext.DefaultTournamentId,
        DisplayName = "FIFA World Cup 2026",
        SportKey    = "football",
        Status      = TournamentStatus.Active,
        StartUtc    = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero),
        EndUtc      = new DateTimeOffset(2026, 7, 19, 23, 59, 59, TimeSpan.Zero),
        CreatedAt   = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    };

    public static async Task SeedAsync(IDocumentStore store, CancellationToken cancellationToken = default)
    {
        await using var session = store.LightweightSession();

        // Idempotency guard: if teams already exist, skip.
        var anyTeam = await session.Query<Team>().AnyAsync(cancellationToken);
        if (anyTeam)
            return;

        // GEN-1 (TWC-35): Ensure the Tournament aggregate exists before seeding child documents.
        session.Store(WorldCup2026);

        // Store teams
        foreach (var team in TeamsData.All)
            session.Store(team);

        // Store groups
        foreach (var group in GroupsData.All)
            session.Store(group);

        // Store fixtures
        foreach (var fixture in FixturesData.All)
            session.Store(fixture);

        // Store knockout slots
        foreach (var slot in KnockoutSlotsData.All)
            session.Store(slot);

        // Store players
        foreach (var player in PlayersData.All)
            session.Store(player);

        // Store initial user profiles
        foreach (var invite in InviteUsersData.All)
            session.Store(invite);

        // Store initial user profiles
        foreach (var profile in UserProfilesData.All)
            session.Store(profile);

        await session.SaveChangesAsync(cancellationToken);
    }
}
