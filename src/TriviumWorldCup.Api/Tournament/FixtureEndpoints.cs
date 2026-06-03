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
        // GET /fixtures/live — fixtures in the live window (InProgress, recent, or imminent).
        // Must be registered before /fixtures to avoid route ambiguity.
        routes.MapGet("/fixtures/live", async (IDocumentSession session, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var recentCutoff  = now.AddHours(-3);   // catch recently completed matches
            var imminentCutoff = now.AddMinutes(30); // upcoming kickoffs within 30 min

            // Load fixtures that are InProgress, OR had kickoff in the last 3 hours,
            // OR have kickoff in the next 30 minutes.
            var fixtures = await session.Query<Fixture>()
                .Where(f =>
                    f.Status == MatchStatus.InProgress ||
                    (f.KickoffUtc >= recentCutoff && f.KickoffUtc <= now) ||
                    (f.KickoffUtc > now && f.KickoffUtc <= imminentCutoff))
                .OrderBy(f => f.KickoffUtc)
                .ThenBy(f => f.MatchNumber)
                .ToListAsync(ct);

            var teams = await session.Query<Team>().ToListAsync(ct);
            var teamMap = teams.ToDictionary(t => t.Id);

            var fixtureDtos = fixtures.Select(f => new FixtureDto(
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
            )).ToList();

            // Load goal events for all fixtures in the response.
            var fixtureIds = fixtures.Select(f => f.Id).ToList();
            List<GoalEventDto> goalDtos;
            if (fixtureIds.Count > 0)
            {
                var goals = await session.Query<GoalEvent>()
                    .Where(g => g.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);

                var playerIds = goals.Select(g => g.PlayerId).Distinct().ToList();
                var players = playerIds.Count > 0
                    ? await session.Query<Player>()
                        .Where(p => p.Id.IsOneOf(playerIds))
                        .ToListAsync(ct)
                    : new System.Collections.Generic.List<Player>();
                var playerMap = players.ToDictionary(p => p.Id);

                goalDtos = goals.Select(g =>
                {
                    playerMap.TryGetValue(g.PlayerId, out var player);
                    return new GoalEventDto(
                        FixtureId:  g.FixtureId,
                        PlayerId:   g.PlayerId,
                        PlayerName: player?.Name ?? g.PlayerId.ToString(),
                        TeamId:     player?.TeamId ?? string.Empty,
                        Type:       g.Type.ToString(),
                        Minute:     g.Minute
                    );
                }).OrderBy(g => g.Minute).ToList();
            }
            else
            {
                goalDtos = new System.Collections.Generic.List<GoalEventDto>();
            }

            // liveWindowActive = true if any fixture is InProgress or kicks off within 30 min.
            var liveWindowActive =
                fixtures.Any(f => f.Status == MatchStatus.InProgress) ||
                fixtures.Any(f => f.KickoffUtc > now && f.KickoffUtc <= imminentCutoff);

            return Results.Ok(new LiveFixturesResponse(
                Fixtures:         fixtureDtos,
                Goals:            goalDtos,
                LiveWindowActive: liveWindowActive
            ));
        })
        .WithName("GetLiveFixtures")
        .WithTags("fixtures")
        .WithSummary("Returns fixtures currently live, recently completed (within 3 h), or kicking off within 30 min, with goal events.");

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

/// <summary>Goal event DTO embedded in the live fixtures response.</summary>
public sealed record GoalEventDto(
    string FixtureId,
    Guid PlayerId,
    string PlayerName,
    string TeamId,
    string Type,
    int Minute);

/// <summary>Response for GET /fixtures/live.</summary>
public sealed record LiveFixturesResponse(
    IReadOnlyList<FixtureDto> Fixtures,
    IReadOnlyList<GoalEventDto> Goals,
    bool LiveWindowActive);
