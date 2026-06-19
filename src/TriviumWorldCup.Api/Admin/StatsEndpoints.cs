using Marten;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Admin;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/admin").WithTags("admin")
            .MapGet("/stats", async (
                HttpContext context,
                IDocumentStore store,
                ITournamentContext tournament,
                CancellationToken ct) =>
            {
                var user = context.GetAppUser();
                if (!user.IsInRole("admin"))
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                await using var session = store.LightweightSession();
                var tid = tournament.TournamentId;

                var profiles        = await session.Query<UserProfile>().ToListAsync(ct);
                var tournamentPreds = await session.Query<TournamentPrediction>().Where(p => p.TournamentId == tid).ToListAsync(ct);
                var groupPreds      = await session.Query<GroupPrediction>().Where(p => p.TournamentId == tid).ToListAsync(ct);
                var knockoutPreds   = await session.Query<KnockoutPrediction>().Where(p => p.TournamentId == tid).ToListAsync(ct);
                var memberScores    = await session.Query<MemberScore>().Where(s => s.TournamentId == tid).ToListAsync(ct);
                var fixtures        = await session.Query<Fixture>().Where(f => f.TournamentId == tid).OrderBy(f => f.KickoffUtc).ToListAsync(ct);
                var players         = await session.Query<Player>().Where(p => p.TournamentId == tid).ToListAsync(ct);
                var teams           = await session.Query<Team>().Where(t => t.TournamentId == tid).ToListAsync(ct);
                var knockoutSlots   = await session.Query<KnockoutSlot>().Where(s => s.TournamentId == tid).ToListAsync(ct);

                var teamById   = teams.ToDictionary(t => t.Id);
                var playerById = players.ToDictionary(p => p.Id);

                // ── Participation ──────────────────────────────────────────────
                var submissionTimeline = tournamentPreds
                    .Where(p => p.SubmittedAt != default)
                    .GroupBy(p => p.SubmittedAt.UtcDateTime.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), count = g.Count() })
                    .ToList();

                var participation = new
                {
                    totalUsers                     = profiles.Count,
                    usersWithTournamentPrediction  = tournamentPreds.Count,
                    usersWithAnyGroupPrediction    = groupPreds.Select(p => p.UserId).Distinct().Count(),
                    totalGroupFixtures             = fixtures.Count,
                    totalGroupPredictionsSubmitted = groupPreds.Count,
                    totalPossibleGroupPredictions  = profiles.Count * fixtures.Count,
                    submissionTimeline,
                };

                // ── Tournament Predictions ─────────────────────────────────────
                var championPicks = tournamentPreds
                    .Where(p => p.ChampionTeamId != null)
                    .GroupBy(p => p.ChampionTeamId!)
                    .Select(g =>
                    {
                        teamById.TryGetValue(g.Key, out var t);
                        return new { teamId = g.Key, teamName = t?.Name ?? g.Key, countryCode = t?.CountryCode, count = g.Count() };
                    })
                    .OrderByDescending(x => x.count)
                    .ToList();

                var allGoldenSixIds = tournamentPreds.SelectMany(p => p.GoldenSixPlayerIds).ToList();

                var topGoldenSixPicks = allGoldenSixIds
                    .GroupBy(id => id)
                    .Select(g =>
                    {
                        playerById.TryGetValue(g.Key, out var p);
                        teamById.TryGetValue(p?.TeamId ?? "", out var t);
                        return new
                        {
                            playerId    = g.Key,
                            playerName  = p?.Name ?? g.Key.ToString(),
                            teamName    = t?.Name ?? p?.TeamId ?? "",
                            teamId      = p?.TeamId ?? "",
                            countryCode = t?.CountryCode,
                            position    = p?.Position.ToString() ?? "?",
                            pickCount   = g.Count(),
                        };
                    })
                    .OrderByDescending(x => x.pickCount)
                    .Take(20)
                    .ToList();

                var goldenSixByPosition = new
                {
                    fwd = allGoldenSixIds.Count(id => playerById.TryGetValue(id, out var p) && p.Position == Position.FWD),
                    mid = allGoldenSixIds.Count(id => playerById.TryGetValue(id, out var p) && p.Position == Position.MID),
                    def = allGoldenSixIds.Count(id => playerById.TryGetValue(id, out var p) && p.Position == Position.DEF),
                    gk  = allGoldenSixIds.Count(id => playerById.TryGetValue(id, out var p) && p.Position == Position.GK),
                };

                var uniqueGoldenSixCombos = tournamentPreds
                    .Where(p => p.GoldenSixPlayerIds.Count == 6)
                    .Select(p => string.Join(",", p.GoldenSixPlayerIds.Select(id => id.ToString()).OrderBy(x => x)))
                    .Distinct()
                    .Count();

                var tournamentPredictions = new
                {
                    championPicks,
                    topGoldenSixPicks,
                    goldenSixByPosition,
                    uniqueGoldenSixCombos,
                };

                // ── Group Stage Predictions ────────────────────────────────────
                var predsByFixture = groupPreds.GroupBy(p => p.FixtureId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var fixtureOutcomes = fixtures
                    .Select(f =>
                    {
                        var preds = predsByFixture.TryGetValue(f.Id, out var list) ? list : [];
                        teamById.TryGetValue(f.HomeTeamId, out var homeTeam);
                        teamById.TryGetValue(f.AwayTeamId, out var awayTeam);
                        return new
                        {
                            fixtureId        = f.Id,
                            matchNumber      = f.MatchNumber,
                            groupLetter      = f.GroupLetter,
                            homeTeamId       = f.HomeTeamId,
                            homeTeamName     = homeTeam?.Name ?? f.HomeTeamId,
                            homeCountryCode  = homeTeam?.CountryCode,
                            awayTeamId       = f.AwayTeamId,
                            awayTeamName     = awayTeam?.Name ?? f.AwayTeamId,
                            awayCountryCode  = awayTeam?.CountryCode,
                            kickoffUtc       = f.KickoffUtc,
                            totalPredictions = preds.Count,
                            homeWinCount     = preds.Count(p => p.HomeScore > p.AwayScore),
                            drawCount        = preds.Count(p => p.HomeScore == p.AwayScore),
                            awayWinCount     = preds.Count(p => p.HomeScore < p.AwayScore),
                            avgHomeScore     = preds.Count > 0 ? Math.Round(preds.Average(p => (double)p.HomeScore), 1) : (double?)null,
                            avgAwayScore     = preds.Count > 0 ? Math.Round(preds.Average(p => (double)p.AwayScore), 1) : (double?)null,
                        };
                    })
                    .ToList();

                var topScorelinesOverall = groupPreds
                    .GroupBy(p => (p.HomeScore, p.AwayScore))
                    .Select(g => new { homeScore = g.Key.HomeScore, awayScore = g.Key.AwayScore, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(10)
                    .ToList();

                var groupPredictions = new { fixtureOutcomes, topScorelinesOverall };

                // ── Knockout Predictions ───────────────────────────────────────
                var knockoutPredsBySlot = knockoutPreds.GroupBy(p => p.SlotKey)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var roundOrder = new Dictionary<Round, int>
                {
                    [Round.R32]        = 0,
                    [Round.R16]        = 1,
                    [Round.QF]         = 2,
                    [Round.SF]         = 3,
                    [Round.ThirdPlace] = 4,
                    [Round.Final]      = 5,
                };

                var slotDistributions = knockoutSlots
                    .OrderBy(s => roundOrder.GetValueOrDefault(s.Round, 99))
                    .ThenBy(s => s.SlotNumber)
                    .Select(slot =>
                    {
                        var preds = knockoutPredsBySlot.TryGetValue(slot.SlotKey, out var list) ? list : [];
                        teamById.TryGetValue(slot.HomeTeamId ?? "", out var homeTeam);
                        teamById.TryGetValue(slot.AwayTeamId ?? "", out var awayTeam);
                        var teamCounts = preds
                            .GroupBy(p => p.PredictedWinnerTeamId)
                            .Select(g =>
                            {
                                teamById.TryGetValue(g.Key, out var t);
                                return new { teamId = g.Key, teamName = t?.Name ?? g.Key, countryCode = t?.CountryCode, count = g.Count() };
                            })
                            .OrderByDescending(x => x.count)
                            .Take(5)
                            .ToList();
                        return new
                        {
                            slotKey          = slot.SlotKey,
                            round            = slot.Round.ToString(),
                            slotNumber       = slot.SlotNumber,
                            homeTeamId       = slot.HomeTeamId,
                            homeTeamName     = homeTeam?.Name,
                            homeCountryCode  = homeTeam?.CountryCode,
                            awayTeamId       = slot.AwayTeamId,
                            awayTeamName     = awayTeam?.Name,
                            awayCountryCode  = awayTeam?.CountryCode,
                            totalPredictions = preds.Count,
                            teamCounts,
                        };
                    })
                    .ToList();

                // Finalist pairs: correlate SF-1 + SF-2 picks per user
                var sfUserPicks = knockoutPreds
                    .Where(p => p.SlotKey is "SF-1" or "SF-2")
                    .GroupBy(p => p.UserId)
                    .Where(g => g.Count() == 2)
                    .Select(g =>
                    {
                        var picks = g.OrderBy(p => p.SlotKey).Select(p => p.PredictedWinnerTeamId).ToArray();
                        return (t1: picks[0], t2: picks[1]);
                    });

                var finalistPairs = sfUserPicks
                    .GroupBy(pair => (
                        a: string.Compare(pair.t1, pair.t2, StringComparison.Ordinal) <= 0 ? pair.t1 : pair.t2,
                        b: string.Compare(pair.t1, pair.t2, StringComparison.Ordinal) <= 0 ? pair.t2 : pair.t1
                    ))
                    .Select(g =>
                    {
                        teamById.TryGetValue(g.Key.a, out var t1);
                        teamById.TryGetValue(g.Key.b, out var t2);
                        return new
                        {
                            team1Id = g.Key.a, team1Name = t1?.Name ?? g.Key.a, team1CountryCode = t1?.CountryCode,
                            team2Id = g.Key.b, team2Name = t2?.Name ?? g.Key.b, team2CountryCode = t2?.CountryCode,
                            count = g.Count(),
                        };
                    })
                    .OrderByDescending(x => x.count)
                    .Take(10)
                    .ToList();

                var knockoutPredictions = new { slotDistributions, finalistPairs };

                // ── Score Stats ────────────────────────────────────────────────
                var scoreValues = memberScores.Select(s => s.TotalPoints).ToList();
                const int bucketSize = 10;
                var maxScore    = scoreValues.Count > 0 ? scoreValues.Max() : 0;
                var bucketCount = maxScore / bucketSize + 1;

                var histogram = Enumerable.Range(0, Math.Max(1, bucketCount))
                    .Select(i => new
                    {
                        label = $"{i * bucketSize}–{(i + 1) * bucketSize - 1}",
                        min   = i * bucketSize,
                        max   = (i + 1) * bucketSize - 1,
                        count = scoreValues.Count(s => s >= i * bucketSize && s < (i + 1) * bucketSize),
                    })
                    .ToList();

                var scores = new
                {
                    histogram,
                    avgTotal            = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.TotalPoints), 1)        : 0,
                    avgGroupPoints      = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.GroupMatchPoints), 1)    : 0,
                    avgKnockoutPoints   = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.KnockoutPoints), 1)      : 0,
                    avgChampionPoints   = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.ChampionPoints), 1)      : 0,
                    avgGoldenSixPoints  = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.GoldenSixPoints), 1)     : 0,
                    avgExactScorelinesCount = memberScores.Count > 0 ? Math.Round(memberScores.Average(s => (double)s.ExactScorelineCount), 1) : 0,
                    maxTotalPoints      = scoreValues.Count > 0 ? scoreValues.Max() : 0,
                    minTotalPoints      = scoreValues.Count > 0 ? scoreValues.Min() : 0,
                };

                return Results.Ok(new { participation, tournamentPredictions, groupPredictions, knockoutPredictions, scores });
            })
            .WithName("GetAdminStats")
            .WithSummary("Aggregated prediction statistics across all users. Admin only.");

        return routes;
    }
}
