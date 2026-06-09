namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Abstraction over the API-Football v3 HTTP client.
/// The production implementation is <see cref="FootballApiClient"/>.
/// A fake implementation is used in unit tests to avoid real HTTP calls.
/// </summary>
public interface IFootballApiClient
{
    Task<IReadOnlyList<ApiFixture>> GetFixturesByDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<ApiGoalEvent>> GetGoalEventsAsync(int fixtureId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiCardEvent>> GetCardEventsAsync(int fixtureId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiSubstitutionEvent>> GetSubstitutionEventsAsync(int fixtureId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiVarEvent>> GetVarEventsAsync(int fixtureId, CancellationToken ct = default);
}
