using Microsoft.Extensions.Configuration;

namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// GEN-1 (TWC-35): Resolves the active tournament for the current request or background job.
///
/// In a single-tournament deployment this always returns the same tournament ID.
/// A future multi-tenant story can swap in a different implementation that reads
/// the tenant from the request host or a header — feature code never changes.
/// </summary>
public interface ITournamentContext
{
    string TournamentId { get; }
}

/// <summary>
/// Default implementation: reads <c>Tournament:ActiveId</c> from configuration,
/// falling back to <see cref="DefaultTournamentId"/> ("world-cup-2026").
/// </summary>
public class SingleTournamentContext(IConfiguration configuration) : ITournamentContext
{
    /// <summary>The slug used for the FIFA World Cup 2026 deployment.</summary>
    public const string DefaultTournamentId = "world-cup-2026";

    public string TournamentId =>
        configuration["Tournament:ActiveId"] ?? DefaultTournamentId;
}
