using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tournament;

/// <summary>
/// Player roster endpoint — publicly readable, no auth required.
/// </summary>
public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /players — all players with embedded team name, sorted by team then name.
        routes.MapGet("/players", async (IDocumentSession session, CancellationToken ct) =>
        {
            var players = await session.Query<Player>()
                .OrderBy(p => p.TeamId)
                .ThenBy(p => p.Name)
                .ToListAsync(ct);

            var teams = await session.Query<Team>().ToListAsync(ct);
            var teamMap = teams.ToDictionary(t => t.Id, t => t.Name);

            var dtos = players.Select(p => new PlayerDto(
                Id:          p.Id,
                Name:        p.Name,
                TeamId:      p.TeamId,
                TeamName:    teamMap.TryGetValue(p.TeamId, out var tn) ? tn : p.TeamId,
                Position:    p.Position.ToString(),
                ShirtNumber: p.ShirtNumber
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetPlayers")
        .WithTags("players")
        .WithSummary("Returns all players in the tournament with their team info.");

        return routes;
    }
}

/// <summary>Player response DTO with embedded team name.</summary>
public sealed record PlayerDto(
    Guid Id,
    string Name,
    string TeamId,
    string TeamName,
    string Position,
    int? ShirtNumber);
