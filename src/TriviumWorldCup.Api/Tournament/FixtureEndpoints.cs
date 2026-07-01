using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Predictions;

namespace TriviumWorldCup.Api.Tournament;

/// <summary>
/// Read-only tournament data endpoints: fixtures and teams.
/// No authentication required — public data.
/// </summary>
public static class FixtureEndpoints
{
    public static IEndpointRouteBuilder MapFixtureEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /fixtures/results — all Completed and Cancelled fixtures (newest first) with goal
        // events and the current user's prediction + computed points per fixture. Cancelled
        // fixtures never score points.
        // Must be registered before /fixtures to avoid route ambiguity.
        routes.MapGet("/fixtures/results", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var fixtures = await session.Query<Fixture>()
                .Where(f => f.Status == MatchStatus.Completed || f.Status == MatchStatus.Cancelled)
                .OrderByDescending(f => f.KickoffUtc)
                .ThenByDescending(f => f.MatchNumber)
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
                KickoffUtc:    f.KickoffUtc,
                Venue:         f.Venue,
                City:          f.City,
                Status:        f.Status.ToString(),
                HomeScore:     f.HomeScore,
                AwayScore:     f.AwayScore,
                ElapsedMinute: f.ElapsedMinute,
                ElapsedExtra:  f.ElapsedExtra
            )).ToList();

            var fixtureIds = fixtures.Select(f => f.Id).ToList();

            List<GoalEventDto> goalDtos;
            List<CardEventDto> cardDtos;
            List<SubstitutionEventDto> subDtos;
            List<VarEventDto> varDtos;
            if (fixtureIds.Count > 0)
            {
                var goals = await session.Query<GoalEvent>()
                    .Where(g => g.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var cards = await session.Query<CardEvent>()
                    .Where(c => c.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var subs = await session.Query<SubstitutionEvent>()
                    .Where(s => s.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var vars = await session.Query<VarEvent>()
                    .Where(v => v.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);

                // Resolve all player names in one query.
                var allPlayerIds = goals.Select(g => g.PlayerId)
                    .Concat(cards.Select(c => c.PlayerId))
                    .Distinct().ToList();
                var playerMap = allPlayerIds.Count > 0
                    ? (await session.Query<Player>().Where(p => p.Id.IsOneOf(allPlayerIds)).ToListAsync(ct))
                        .ToDictionary(p => p.Id)
                    : new Dictionary<Guid, Player>();

                goalDtos = goals.Select(g =>
                {
                    playerMap.TryGetValue(g.PlayerId, out var player);
                    return new GoalEventDto(g.FixtureId, g.PlayerId,
                        player?.Name ?? g.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                        g.Type.ToString(), g.Minute, g.ExtraMinute);
                }).OrderBy(g => g.Minute).ToList();

                cardDtos = cards.Select(c =>
                {
                    playerMap.TryGetValue(c.PlayerId, out var player);
                    return new CardEventDto(c.FixtureId, c.PlayerId,
                        player?.Name ?? c.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                        c.Type.ToString(), c.Minute, c.ExtraMinute);
                }).OrderBy(c => c.Minute).ToList();

                subDtos = subs.Select(s => new SubstitutionEventDto(
                    s.FixtureId, s.PlayerInName, s.PlayerOutName, s.TeamId, s.Minute, s.ExtraMinute
                )).OrderBy(s => s.Minute).ToList();

                varDtos = vars.Select(v => new VarEventDto(
                    v.FixtureId, v.PlayerName, v.TeamId, v.Type.ToString(), v.Minute, v.ExtraMinute
                )).OrderBy(v => v.Minute).ToList();
            }
            else
            {
                goalDtos = new List<GoalEventDto>();
                cardDtos = new List<CardEventDto>();
                subDtos = new List<SubstitutionEventDto>();
                varDtos = new List<VarEventDto>();
            }

            // Load the current user's predictions for completed fixtures with points from MemberScore.
            List<MyFixturePredictionDto> myPredictions;
            if (fixtureIds.Count > 0)
            {
                var predictions = await session.Query<GroupPrediction>()
                    .Where(p => p.UserId == user.UserId && p.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);

                var memberScore = await session.LoadAsync<MemberScore>(user.UserId, ct);
                var breakdownByFixture = memberScore?.GroupBreakdown.ToDictionary(b => b.FixtureId)
                                          ?? new Dictionary<string, GroupPredictionScore>();

                var fixtureById = fixtures.ToDictionary(f => f.Id);
                myPredictions = predictions
                    .Where(p => fixtureById.TryGetValue(p.FixtureId, out var f) && f.HomeScore.HasValue && f.AwayScore.HasValue)
                    .Select(p =>
                    {
                        var points = breakdownByFixture.GetValueOrDefault(p.FixtureId)?.Points ?? 0;
                        return new MyFixturePredictionDto(p.FixtureId, p.HomeScore, p.AwayScore, points);
                    })
                    .ToList();
            }
            else
            {
                myPredictions = new List<MyFixturePredictionDto>();
            }

            return Results.Ok(new FixtureResultsResponse(fixtureDtos, goalDtos, cardDtos, subDtos, varDtos, myPredictions));
        })
        .WithName("GetFixtureResults")
        .WithTags("fixtures")
        .WithSummary("Returns all completed fixtures (newest first) with goal events and the current user's prediction and points.");

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

            // Load knockout slots in the same live window.
            var knockoutSlots = await session.Query<KnockoutSlot>()
                .Where(s =>
                    s.Status == MatchStatus.InProgress ||
                    s.Status == MatchStatus.ExtraTime ||
                    s.Status == MatchStatus.PenaltyShootout ||
                    (s.KickoffUtc >= recentCutoff && s.KickoffUtc <= now) ||
                    (s.KickoffUtc > now && s.KickoffUtc <= imminentCutoff))
                .OrderBy(s => s.KickoffUtc)
                .ThenBy(s => s.SlotNumber)
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
                KickoffUtc:    f.KickoffUtc,
                Venue:         f.Venue,
                City:          f.City,
                Status:        f.Status.ToString(),
                HomeScore:     f.HomeScore,
                AwayScore:     f.AwayScore,
                ElapsedMinute: f.ElapsedMinute,
                ElapsedExtra:  f.ElapsedExtra
            )).ToList();

            var knockoutDtos = knockoutSlots.Select(s => new LiveKnockoutSlotDto(
                SlotKey:          s.SlotKey,
                Round:            s.Round.ToString(),
                SlotNumber:       s.SlotNumber,
                HomeTeamId:       s.HomeTeamId,
                HomeTeamName:     s.HomeTeamId != null && teamMap.TryGetValue(s.HomeTeamId, out var kht) ? kht.Name : s.HomeTeamId,
                AwayTeamId:       s.AwayTeamId,
                AwayTeamName:     s.AwayTeamId != null && teamMap.TryGetValue(s.AwayTeamId, out var kat) ? kat.Name : s.AwayTeamId,
                KickoffUtc:       s.KickoffUtc,
                Venue:            s.Venue,
                City:             s.City,
                Status:           s.Status.ToString(),
                HomeScore:        s.HomeScore,
                AwayScore:        s.AwayScore,
                PenaltyHomeScore: s.PenaltyHomeScore,
                PenaltyAwayScore: s.PenaltyAwayScore,
                WinnerTeamId:     s.WinnerTeamId
            )).ToList();

            // Load goal, card, substitution, and VAR events for all fixtures in the response.
            // Knockout slot events are keyed by SlotKey (e.g. "R32-1") — same query handles both.
            var fixtureIds = fixtures.Select(f => f.Id)
                .Concat(knockoutSlots.Select(s => s.SlotKey))
                .ToList();
            List<GoalEventDto> goalDtos;
            List<CardEventDto> cardDtos;
            List<SubstitutionEventDto> subDtos;
            List<VarEventDto> varDtos;
            if (fixtureIds.Count > 0)
            {
                var goals = await session.Query<GoalEvent>()
                    .Where(g => g.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var cards = await session.Query<CardEvent>()
                    .Where(c => c.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var subs = await session.Query<SubstitutionEvent>()
                    .Where(s => s.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);
                var vars = await session.Query<VarEvent>()
                    .Where(v => v.FixtureId.IsOneOf(fixtureIds))
                    .ToListAsync(ct);

                var allPlayerIds = goals.Select(g => g.PlayerId)
                    .Concat(cards.Select(c => c.PlayerId))
                    .Distinct().ToList();
                var playerMap = allPlayerIds.Count > 0
                    ? (await session.Query<Player>().Where(p => p.Id.IsOneOf(allPlayerIds)).ToListAsync(ct))
                        .ToDictionary(p => p.Id)
                    : new Dictionary<Guid, Player>();

                goalDtos = goals.Select(g =>
                {
                    playerMap.TryGetValue(g.PlayerId, out var player);
                    return new GoalEventDto(g.FixtureId, g.PlayerId,
                        player?.Name ?? g.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                        g.Type.ToString(), g.Minute, g.ExtraMinute);
                }).OrderBy(g => g.Minute).ToList();

                cardDtos = cards.Select(c =>
                {
                    playerMap.TryGetValue(c.PlayerId, out var player);
                    return new CardEventDto(c.FixtureId, c.PlayerId,
                        player?.Name ?? c.PlayerId.ToString(), player?.TeamId ?? string.Empty,
                        c.Type.ToString(), c.Minute, c.ExtraMinute);
                }).OrderBy(c => c.Minute).ToList();

                subDtos = subs.Select(s => new SubstitutionEventDto(
                    s.FixtureId, s.PlayerInName, s.PlayerOutName, s.TeamId, s.Minute, s.ExtraMinute
                )).OrderBy(s => s.Minute).ToList();

                varDtos = vars.Select(v => new VarEventDto(
                    v.FixtureId, v.PlayerName, v.TeamId, v.Type.ToString(), v.Minute, v.ExtraMinute
                )).OrderBy(v => v.Minute).ToList();
            }
            else
            {
                goalDtos = new List<GoalEventDto>();
                cardDtos = new List<CardEventDto>();
                subDtos = new List<SubstitutionEventDto>();
                varDtos = new List<VarEventDto>();
            }

            // liveWindowActive = true when polling should continue:
            // - any fixture/slot is InProgress / ExtraTime / PenaltyShootout
            // - any fixture/slot kicks off within the next 30 min
            // - any non-completed fixture kicked off in the last 30 min (covers the
            //   Scheduled→InProgress gap between kickoff and the first job cycle)
            var liveWindowActive =
                fixtures.Any(f => f.Status is MatchStatus.InProgress
                                           or MatchStatus.ExtraTime
                                           or MatchStatus.PenaltyShootout) ||
                knockoutSlots.Any(s => s.Status is MatchStatus.InProgress
                                                or MatchStatus.ExtraTime
                                                or MatchStatus.PenaltyShootout) ||
                fixtures.Any(f => f.KickoffUtc > now && f.KickoffUtc <= imminentCutoff) ||
                knockoutSlots.Any(s => s.KickoffUtc > now && s.KickoffUtc <= imminentCutoff) ||
                fixtures.Any(f => f.KickoffUtc > now.AddMinutes(-30) && f.KickoffUtc <= now &&
                                  f.Status != MatchStatus.Completed &&
                                  f.Status != MatchStatus.Cancelled) ||
                knockoutSlots.Any(s => s.KickoffUtc > now.AddMinutes(-30) && s.KickoffUtc <= now &&
                                  s.Status != MatchStatus.Completed &&
                                  s.Status != MatchStatus.Cancelled);

            return Results.Ok(new LiveFixturesResponse(
                Fixtures:       fixtureDtos,
                Goals:          goalDtos,
                Cards:          cardDtos,
                Substitutions:  subDtos,
                VarEvents:      varDtos,
                LiveWindowActive: liveWindowActive,
                KnockoutSlots:  knockoutDtos
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
                KickoffUtc:    f.KickoffUtc,
                Venue:         f.Venue,
                City:          f.City,
                Status:        f.Status.ToString(),
                HomeScore:     f.HomeScore,
                AwayScore:     f.AwayScore,
                ElapsedMinute: f.ElapsedMinute,
                ElapsedExtra:  f.ElapsedExtra
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetFixtures")
        .WithTags("fixtures")
        .WithSummary("Returns all group-stage fixtures with embedded team names.")
        .CacheOutput("fixtures");

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
        .WithSummary("Returns all 48 teams.")
        .CacheOutput("teams");

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
    int? AwayScore,
    int? ElapsedMinute,
    int? ElapsedExtra);

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
    int Minute,
    int? ExtraMinute);

/// <summary>Card event DTO (yellow / second yellow / red) embedded in fixture responses.</summary>
public sealed record CardEventDto(
    string FixtureId,
    Guid PlayerId,
    string PlayerName,
    string TeamId,
    string Type,
    int Minute,
    int? ExtraMinute);

/// <summary>Substitution event DTO embedded in fixture responses.</summary>
public sealed record SubstitutionEventDto(
    string FixtureId,
    string PlayerInName,
    string PlayerOutName,
    string TeamId,
    int Minute,
    int? ExtraMinute);

/// <summary>VAR decision DTO embedded in fixture responses.</summary>
public sealed record VarEventDto(
    string FixtureId,
    string PlayerName,
    string TeamId,
    string Type,
    int Minute,
    int? ExtraMinute);

/// <summary>Knockout slot DTO embedded in the live fixtures response (includes resolved team names).</summary>
public sealed record LiveKnockoutSlotDto(
    string SlotKey,
    string Round,
    int SlotNumber,
    string? HomeTeamId,
    string? HomeTeamName,
    string? AwayTeamId,
    string? AwayTeamName,
    DateTimeOffset? KickoffUtc,
    string? Venue,
    string? City,
    string Status,
    int? HomeScore,
    int? AwayScore,
    int? PenaltyHomeScore,
    int? PenaltyAwayScore,
    string? WinnerTeamId);

/// <summary>Response for GET /fixtures/live.</summary>
public sealed record LiveFixturesResponse(
    IReadOnlyList<FixtureDto> Fixtures,
    IReadOnlyList<GoalEventDto> Goals,
    IReadOnlyList<CardEventDto> Cards,
    IReadOnlyList<SubstitutionEventDto> Substitutions,
    IReadOnlyList<VarEventDto> VarEvents,
    bool LiveWindowActive,
    IReadOnlyList<LiveKnockoutSlotDto> KnockoutSlots);

/// <summary>Per-fixture prediction result for the current user, returned by GET /fixtures/results.</summary>
public sealed record MyFixturePredictionDto(
    string FixtureId,
    int PredictedHome,
    int PredictedAway,
    int Points);

/// <summary>Response for GET /fixtures/results.</summary>
public sealed record FixtureResultsResponse(
    IReadOnlyList<FixtureDto> Fixtures,
    IReadOnlyList<GoalEventDto> Goals,
    IReadOnlyList<CardEventDto> Cards,
    IReadOnlyList<SubstitutionEventDto> Substitutions,
    IReadOnlyList<VarEventDto> VarEvents,
    IReadOnlyList<MyFixturePredictionDto> MyPredictions);
