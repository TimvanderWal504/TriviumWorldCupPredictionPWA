using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Singleton cache of the player roster. Loaded once on first use since the roster
/// is static for the duration of the tournament. Thread-safe double-checked init.
/// </summary>
public sealed class PlayerCache(IDocumentStore store, ITournamentContext tournamentContext, ILogger<PlayerCache> logger)
{
    private IReadOnlyDictionary<string, Player>? _byFullName;
    private ILookup<string, Player>? _byLastName;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public IReadOnlyDictionary<string, Player> ByFullName => _byFullName
        ?? throw new InvalidOperationException("PlayerCache not yet loaded — call EnsureLoadedAsync first.");

    public ILookup<string, Player> ByLastName => _byLastName
        ?? throw new InvalidOperationException("PlayerCache not yet loaded — call EnsureLoadedAsync first.");

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_byFullName is not null) return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_byFullName is not null) return;

            await using var session = store.LightweightSession();
            var players = await session.Query<Player>()
                .Where(p => p.TournamentId == tournamentContext.TournamentId)
                .ToListAsync(ct);

            _byFullName = players
                .GroupBy(p => ResultIngestionJob.NormalizeName(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _byLastName = players
                .ToLookup(p => ResultIngestionJob.LastWord(p.Name), StringComparer.OrdinalIgnoreCase);

            logger.LogInformation("PlayerCache: loaded {Count} players", players.Count);
        }
        finally
        {
            _lock.Release();
        }
    }
}
