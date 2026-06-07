using System.Text.Json;
using System.Text.Json.Serialization;

namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Typed HTTP client wrapping the API-Football v3 API (https://v3.football.api-sports.io).
///
/// SPIKE FINDINGS (TWC-9):
///   Endpoint: GET /leagues?id=1&amp;season=2026
///   - League ID 1 is "World Cup" on API-Football; season 2026 is listed as available.
///   - The endpoint returns league metadata including country "World" and type "Cup".
///
///   Endpoint: GET /fixtures?league=1&amp;season=2026
///   - Returns all 72 group-stage + 16 R32 + 8 R16 + 4 QF + 2 SF + 2 final-round fixtures.
///   - Each fixture includes: fixture.id (int), fixture.date (ISO 8601 UTC), fixture.status.short
///     ("NS", "1H", "HT", "2H", "FT", "PEN", etc.), teams.home.id / teams.home.name,
///     teams.away.id / teams.away.name, goals.home (int?), goals.away (int?).
///   - As of 2 June 2026, group-stage matches from 11 June onward have status "NS" (not started);
///     completed matches will carry status "FT". Data is confirmed to be live for 2026.
///
///   Endpoint: GET /fixtures/events?fixture={id}&amp;type=Goal
///   - Verified against completed World Cup historical fixtures (e.g. WC 2022).
///   - Response includes: player.id (int), player.name (string), assist.name (string),
///     time.elapsed (int — minute), detail ("Normal Goal", "Penalty", "Own Goal").
///   - Player position is NOT returned in the events endpoint — it is returned by
///     GET /players?league=1&amp;season=2026 (player.statistics[0].games.position: "G","D","M","F").
///   - For TWC-9, player position is already stored on the Player Marten document from seed data
///     (TWC-5), so position mapping from the API is not needed at ingest time.
///   - Goal scorer name is matched to the app's Player documents by exact name match.
///   - If 2026 fixture data is not yet available for a specific match (match not yet played),
///     GetGoalEventsAsync will return an empty list — this is safe and idempotent.
///
/// Auth: reads Football:ApiKey from IConfiguration; sets x-apisports-key header.
/// Never commit the key value.
/// </summary>
public class FootballApiClient : IFootballApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // API-Football league ID for FIFA World Cup
    private const int LeagueId = 1;
    private const int Season = 2026;

    public FootballApiClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Returns all group-stage fixtures (league=1, season=2026) regardless of status.
    /// Used to detect live windows and check which fixtures need ingesting.
    /// </summary>
    public async Task<IReadOnlyList<ApiFixture>> GetAllGroupFixturesAsync(CancellationToken ct = default)
    {
        return await FetchFixturesAsync($"fixtures?league={LeagueId}&season={Season}", ct);
    }

    /// <summary>
    /// Returns only fixtures with status FT (full time) for the group stage.
    /// Convenience wrapper for callers that only need completed matches.
    /// </summary>
    public async Task<IReadOnlyList<ApiFixture>> GetCompletedGroupFixturesAsync(CancellationToken ct = default)
    {
        return await FetchFixturesAsync($"fixtures?league={LeagueId}&season={Season}&status=FT", ct);
    }

    /// <summary>
    /// Returns all goal events for a given API fixture ID.
    /// </summary>
    public async Task<IReadOnlyList<ApiGoalEvent>> GetGoalEventsAsync(int fixtureId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"fixtures/events?fixture={fixtureId}&type=Goal", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapper = JsonSerializer.Deserialize<ApiResponse<ApiGoalEvent>>(body, JsonOptions);

        return wrapper?.Response ?? [];
    }

    private async Task<IReadOnlyList<ApiFixture>> FetchFixturesAsync(string path, CancellationToken ct)
    {
        var response = await _http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapper = JsonSerializer.Deserialize<ApiResponse<ApiFixtureWrapper>>(body, JsonOptions);

        if (wrapper?.Response == null)
            return [];

        return wrapper.Response
            .Where(w => w.Fixture != null)
            .Select(w => new ApiFixture
            {
                FixtureId   = w.Fixture!.Id,
                Date        = w.Fixture.Date,
                StatusShort = w.Fixture.Status?.Short ?? "NS",
                HomeTeamId  = w.Teams?.Home?.Id ?? 0,
                HomeTeamName = w.Teams?.Home?.Name ?? string.Empty,
                AwayTeamId  = w.Teams?.Away?.Id ?? 0,
                AwayTeamName = w.Teams?.Away?.Name ?? string.Empty,
                HomeGoals         = w.Goals?.Home,
                AwayGoals         = w.Goals?.Away,
                ScoreFullTimeHome = w.Score?.Fulltime?.Home,
                ScoreFullTimeAway = w.Score?.Fulltime?.Away,
                ScorePenaltyHome  = w.Score?.Penalty?.Home,
                ScorePenaltyAway  = w.Score?.Penalty?.Away,
            })
            .ToList();
    }
}

// ── Minimal DTOs matching the API-Football v3 JSON shape ─────────────────────

/// <summary>Top-level API-Football response wrapper.</summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("response")]
    public List<T>? Response { get; set; }
}

/// <summary>Flattened fixture data, extracted from the nested API response.</summary>
public sealed class ApiFixture
{
    public int      FixtureId    { get; set; }
    public string   Date         { get; set; } = string.Empty;
    /// <summary>API status short code: "NS", "1H", "HT", "2H", "FT", "PEN", "AET", etc.</summary>
    public string   StatusShort  { get; set; } = "NS";
    public int      HomeTeamId   { get; set; }
    public string   HomeTeamName { get; set; } = string.Empty;
    public int      AwayTeamId   { get; set; }
    public string   AwayTeamName { get; set; } = string.Empty;
    /// <summary>Running total goals at the current moment (includes ET goals, excludes penalty shootout).</summary>
    public int?     HomeGoals           { get; set; }
    public int?     AwayGoals           { get; set; }
    /// <summary>90-minute score from score.fulltime — null while the match is still in progress.</summary>
    public int?     ScoreFullTimeHome   { get; set; }
    public int?     ScoreFullTimeAway   { get; set; }
    /// <summary>Penalty shootout score from score.penalty — non-null when StatusShort is "P" or "PEN".</summary>
    public int?     ScorePenaltyHome    { get; set; }
    public int?     ScorePenaltyAway    { get; set; }

    /// <summary>Returns true if this fixture is finished (FT, PEN, or AET).</summary>
    public bool IsFullTime => StatusShort is "FT" or "PEN" or "AET";

    /// <summary>Returns true if this fixture is currently being played.</summary>
    public bool IsLive => StatusShort is "1H" or "HT" or "2H" or "ET" or "BT" or "P";
}

/// <summary>Goal event returned from /fixtures/events.</summary>
public sealed class ApiGoalEvent
{
    [JsonPropertyName("time")]
    public ApiTime? Time { get; set; }

    [JsonPropertyName("player")]
    public ApiPlayer? Player { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// "Normal Goal" → OpenPlay
    /// "Penalty"      → PenaltyInMatch
    /// "Own Goal"     → OwnGoal
    /// Anything else  → OpenPlay (safe fallback)
    /// </summary>
    public bool IsOwnGoal => string.Equals(Detail, "Own Goal", StringComparison.OrdinalIgnoreCase);
    public bool IsPenalty => string.Equals(Detail, "Penalty", StringComparison.OrdinalIgnoreCase);
}

public sealed class ApiTime
{
    [JsonPropertyName("elapsed")]
    public int Elapsed { get; set; }
}

public sealed class ApiPlayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// ── Internal nested DTOs used only while deserialising fixtures ───────────────

internal sealed class ApiFixtureWrapper
{
    [JsonPropertyName("fixture")]
    public ApiFixtureDetail? Fixture { get; set; }

    [JsonPropertyName("teams")]
    public ApiTeams? Teams { get; set; }

    [JsonPropertyName("goals")]
    public ApiGoals? Goals { get; set; }

    [JsonPropertyName("score")]
    public ApiScore? Score { get; set; }
}

internal sealed class ApiFixtureDetail
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public ApiStatus? Status { get; set; }
}

internal sealed class ApiStatus
{
    [JsonPropertyName("short")]
    public string? Short { get; set; }
}

internal sealed class ApiTeams
{
    [JsonPropertyName("home")]
    public ApiTeam? Home { get; set; }

    [JsonPropertyName("away")]
    public ApiTeam? Away { get; set; }
}

internal sealed class ApiTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class ApiGoals
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}

internal sealed class ApiScore
{
    [JsonPropertyName("fulltime")]
    public ApiScoreEntry? Fulltime { get; set; }

    [JsonPropertyName("penalty")]
    public ApiScoreEntry? Penalty { get; set; }
}

internal sealed class ApiScoreEntry
{
    [JsonPropertyName("home")]
    public int? Home { get; set; }

    [JsonPropertyName("away")]
    public int? Away { get; set; }
}
