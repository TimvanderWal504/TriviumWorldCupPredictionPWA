using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tournament;

/// <summary>
/// Read-only tournament data endpoints: fixtures and teams.
/// No authentication required — public data.
/// </summary>
public static class FixtureEndpoints
{
    public static IEndpointRouteBuilder MapFixtureEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /fixtures — all 72 group-stage fixtures with embedded team names.
        routes.MapGet("/fixtures", async (IDocumentSession session, CancellationToken ct) =>
        {
            var fixtures = await session.Query<Fixture>()
                .OrderBy(f => f.KickoffUtc)
                .ThenBy(f => f.MatchNumber)
                .ToListAsync(ct);

            var teams = await session.Query<Team>().ToListAsync(ct);
            var teamMap = teams.ToDictionary(t => t.Id);

            var dtos = fixtures.Select(f => new FixtureDto(
                Id:           f.Id,
                MatchNumber:  f.MatchNumber,
                GroupLetter:  f.GroupLetter,
                HomeTeamId:   f.HomeTeamId,
                HomeTeamName: teamMap.TryGetValue(f.HomeTeamId, out var ht) ? ht.Name : f.HomeTeamId,
                AwayTeamId:   f.AwayTeamId,
                AwayTeamName: teamMap.TryGetValue(f.AwayTeamId, out var at) ? at.Name : f.AwayTeamId,
                KickoffUtc:   f.KickoffUtc,
                Venue:        f.Venue,
                City:         f.City,
                Status:       f.Status.ToString(),
                HomeScore:    f.HomeScore,
                AwayScore:    f.AwayScore
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetFixtures")
        .WithTags("fixtures")
        .WithSummary("Returns all group-stage fixtures with embedded team names.");

        // GET /teams — all 48 teams.
        routes.MapGet("/teams", async (IDocumentSession session, CancellationToken ct) =>
        {
            var teams = await session.Query<Team>()
                .OrderBy(t => t.GroupLetter)
                .ThenBy(t => t.Name)
                .ToListAsync(ct);

            var dtos = teams.Select(t => new TeamDto(
                Id:          t.Id,
                Name:        t.Name,
                FifaCode:    t.FifaCode,
                CountryCode: t.CountryCode
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetTeams")
        .WithTags("fixtures")
        .WithSummary("Returns all 48 teams.");

        return routes;
    }
}

/// <summary>Fixture response DTO with embedded team names.</summary>
public sealed record FixtureDto(
    string Id,
    int MatchNumber,
    string GroupLetter,
    string HomeTeamId,
    string HomeTeamName,
    string AwayTeamId,
    string AwayTeamName,
    DateTimeOffset KickoffUtc,
    string Venue,
    string City,
    string Status,
    int? HomeScore,
    int? AwayScore);

/// <summary>Team response DTO.</summary>
public sealed record TeamDto(
    string Id,
    string Name,
    string FifaCode,
    string CountryCode);
