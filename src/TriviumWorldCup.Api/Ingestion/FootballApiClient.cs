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
    private readonly FootballApiBudget _budget;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // API-Football league ID for FIFA World Cup
    private const int LeagueId = 1;
    private const int Season = 2026;

    public FootballApiClient(HttpClient http, FootballApiBudget? budget = null)
    {
        _http = http;
        // Null only in tests that construct the client directly; a disabled budget is a no-op.
        _budget = budget ?? FootballApiBudget.Disabled;
    }

    /// <summary>
    /// Returns all fixtures scheduled on the given UTC date (league=1, season=2026).
    /// Replaces the full-season fetch — a single date returns at most 5–6 fixtures,
    /// keeping each polling cycle cheap regardless of poll interval.
    /// </summary>
    public async Task<IReadOnlyList<ApiFixture>> GetFixturesByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return await FetchFixturesAsync($"fixtures?league={LeagueId}&season={Season}&date={date:yyyy-MM-dd}", ct);
    }

    /// <summary>
    /// Returns all fixtures for the full 2026 season (league=1) in one call — 104 fixtures
    /// covering group stage + all knockout rounds. Used for the one-time backfill of
    /// FootballApiFixtureId values; not used in the regular polling cycle.
    /// Costs 1 API request against the daily quota.
    /// </summary>
    public async Task<IReadOnlyList<ApiFixture>> GetAllFixturesForSeasonAsync(CancellationToken ct = default)
    {
        return await FetchFixturesAsync($"fixtures?league={LeagueId}&season={Season}", ct);
    }

    /// <summary>
    /// Returns all match events (goals, cards, substitutions, VAR decisions) for a given fixture
    /// in a single request. Each event carries a <see cref="ApiMatchEvent.Type"/> field
    /// ("Goal", "Card", "subst", "Var") so callers can split by category locally.
    /// Replaces the four typed calls — saves 3 API requests per completed fixture.
    /// </summary>
    public async Task<IReadOnlyList<ApiMatchEvent>> GetAllEventsAsync(int fixtureId, CancellationToken ct = default)
    {
        ThrowIfBudgetExhausted();

        var response = await _http.GetAsync($"fixtures/events?fixture={fixtureId}", ct);

        ThrowIfQuotaExceeded(response);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapper = JsonSerializer.Deserialize<ApiResponse<ApiMatchEvent>>(body, JsonOptions);

        return wrapper?.Response ?? [];
    }

    /// <summary>
    /// Detects HTTP 429 (quota exhaustion) and throws the same recognisable exception shape
    /// used everywhere in the ingestion pipeline: an <see cref="HttpRequestException"/> whose
    /// InnerException is an <see cref="InvalidOperationException"/> with message
    /// "Quota exceeded". ResultIngestionJob's catch clauses (and RecheckPostponedFixturesAsync)
    /// pattern-match on exactly this shape to report quota exhaustion consistently regardless
    /// of which underlying API call (events or fixtures) hit the limit.
    /// </summary>
    private static void ThrowIfQuotaExceeded(HttpResponseMessage response)
    {
        if ((int)response.StatusCode != 429) return;

        var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds
                      ?? response.Headers.RetryAfter?.Date?.Subtract(DateTimeOffset.UtcNow).TotalSeconds
                      ?? 60;
        throw new HttpRequestException(
            $"API-Football quota exhausted (HTTP 429). Retry after {retryAfter}s. Daily quota resets at 00:00 UTC.",
            new InvalidOperationException("Quota exceeded"));
    }

    /// <summary>
    /// Consumes one call from the free-plan budget before an API request is made. When budget mode
    /// is active and the daily cap has been reached, throws the same recognisable "Quota exceeded"
    /// shape that <see cref="ThrowIfQuotaExceeded"/> raises on an HTTP 429, so the ingestion job's
    /// existing catch clauses record it and defer to the next cycle. No-op in disabled mode.
    /// </summary>
    private void ThrowIfBudgetExhausted()
    {
        if (_budget.TryConsumeCall()) return;
        throw new HttpRequestException(
            $"Football API daily budget of {_budget.MaxCallsPerDay} calls reached — deferring until the 00:00 UTC reset.",
            new InvalidOperationException("Quota exceeded"));
    }

    private async Task<IReadOnlyList<ApiFixture>> FetchFixturesAsync(string path, CancellationToken ct)
    {
        ThrowIfBudgetExhausted();

        var response = await _http.GetAsync(path, ct);

        ThrowIfQuotaExceeded(response);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        var wrapper = JsonSerializer.Deserialize<ApiResponse<ApiFixtureWrapper>>(body, JsonOptions);

        if (wrapper?.Response == null)
            return [];

        return wrapper.Response
            .Where(w => w.Fixture != null)
            .Select(w => new ApiFixture
            {
                FixtureId      = w.Fixture!.Id,
                Date           = w.Fixture.Date,
                StatusShort    = w.Fixture.Status?.Short ?? "NS",
                StatusElapsed  = w.Fixture.Status?.Elapsed,
                StatusExtra    = w.Fixture.Status?.Extra,
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

    /// <summary>Elapsed match minute from the API's status.elapsed (null when not live).</summary>
    public int? StatusElapsed { get; set; }
    /// <summary>Stoppage-time extra minutes from status.extra (e.g. 2 for "45+2'").</summary>
    public int? StatusExtra { get; set; }

    /// <summary>Returns true if this fixture is finished (FT, PEN, or AET).</summary>
    public bool IsFullTime => StatusShort is "FT" or "PEN" or "AET";

    /// <summary>Returns true if this fixture is currently being played.</summary>
    public bool IsLive => StatusShort is "1H" or "HT" or "2H" or "ET" or "BT" or "P";

    /// <summary>Returns true if the API reports this fixture as delayed to an as-yet-unknown new kickoff time.</summary>
    public bool IsPostponed => StatusShort is "PST";

    /// <summary>Returns true if the API reports this fixture as cancelled, abandoned, suspended, or awarded — it will not resume.</summary>
    public bool IsCancelled => StatusShort is "CANC" or "ABD" or "AWD" or "WO" or "SUSP";

    /// <summary>Returns true if the fixture is off its originally scheduled course — postponed or cancelled.</summary>
    public bool IsCancelledOrPostponed => IsPostponed || IsCancelled;
}

/// <summary>
/// A single match event returned from /fixtures/events (no type filter applied).
/// The <see cref="Type"/> field discriminates the four categories; use the IsGoal / IsCard /
/// IsSub / IsVar helpers to split after a single fetch rather than making four typed calls.
/// API quirk for substitutions: <see cref="Player"/> = player going OFF, <see cref="Assist"/> = player coming ON.
/// </summary>
public sealed class ApiMatchEvent
{
    /// <summary>Event category: "Goal", "Card", "subst", "Var".</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("time")]
    public ApiTime? Time { get; set; }

    [JsonPropertyName("team")]
    public ApiTeam? Team { get; set; }

    /// <summary>For goals/cards/VAR: the player involved. For subs: the player going OFF.</summary>
    [JsonPropertyName("player")]
    public ApiPlayer? Player { get; set; }

    /// <summary>For subs: the player coming ON (API reuses the assist field).</summary>
    [JsonPropertyName("assist")]
    public ApiPlayer? Assist { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>
    /// Free-text qualifier from API-Football. For penalty shootout kicks this is
    /// "Penalty Shootout" — the ONLY field that distinguishes a shootout kick from a
    /// regulation penalty (both arrive as type:"Goal", detail:"Penalty").
    /// </summary>
    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    // ── Type discriminators ──────────────────────────────────────────────────
    public bool IsGoal => string.Equals(Type, "Goal",  StringComparison.OrdinalIgnoreCase);
    public bool IsCard => string.Equals(Type, "Card",  StringComparison.OrdinalIgnoreCase);
    public bool IsSub  => string.Equals(Type, "subst", StringComparison.OrdinalIgnoreCase);
    public bool IsVar  => string.Equals(Type, "Var",   StringComparison.OrdinalIgnoreCase);

    // ── Goal detail helpers ──────────────────────────────────────────────────
    public bool IsOwnGoal      => string.Equals(Detail, "Own Goal",        StringComparison.OrdinalIgnoreCase);
    public bool IsPenalty      => string.Equals(Detail, "Penalty",         StringComparison.OrdinalIgnoreCase);
    // API-Football reports a missed penalty under type:"Goal" distinguished only by this detail value.
    // A missed penalty must never reach goal storage — the shooter didn't score.
    public bool IsMissedPenalty => string.Equals(Detail, "Missed Penalty", StringComparison.OrdinalIgnoreCase);
    // A scored shootout kick: type:"Goal", detail:"Penalty", comments:"Penalty Shootout".
    // (Missed shootout kicks are caught by IsMissedPenalty and never reach goal storage.)
    public bool IsShootout => string.Equals(Comments, "Penalty Shootout", StringComparison.OrdinalIgnoreCase);

    // ── Card detail helpers ──────────────────────────────────────────────────
    public bool IsYellow       => string.Equals(Detail, "Yellow Card",   StringComparison.OrdinalIgnoreCase);
    public bool IsSecondYellow => string.Equals(Detail, "Second Yellow", StringComparison.OrdinalIgnoreCase);
    public bool IsRed          => string.Equals(Detail, "Red Card",      StringComparison.OrdinalIgnoreCase);

    // ── VAR detail helpers ───────────────────────────────────────────────────
    public bool IsGoalCancelled     =>
        Detail is { Length: > 0 } d &&
        (d.StartsWith("Goal cancelled",  StringComparison.OrdinalIgnoreCase) ||
         d.StartsWith("Goal Disallowed", StringComparison.OrdinalIgnoreCase));
    public bool IsCardUpgradeRed    => string.Equals(Detail, "Card Upgrade - Red Card",      StringComparison.OrdinalIgnoreCase);
    public bool IsCardUpgrade2ndYel => string.Equals(Detail, "Card Upgrade - Second Yellow", StringComparison.OrdinalIgnoreCase);
}

public sealed class ApiTime
{
    [JsonPropertyName("elapsed")]
    public int Elapsed { get; set; }

    /// <summary>Stoppage-time minutes within the period (e.g. 2 for "45+2").</summary>
    [JsonPropertyName("extra")]
    public int? Extra { get; set; }
}

public sealed class ApiPlayer
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

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

    [JsonPropertyName("elapsed")]
    public int? Elapsed { get; set; }

    [JsonPropertyName("extra")]
    public int? Extra { get; set; }
}

internal sealed class ApiTeams
{
    [JsonPropertyName("home")]
    public ApiTeam? Home { get; set; }

    [JsonPropertyName("away")]
    public ApiTeam? Away { get; set; }
}

public sealed class ApiTeam
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
