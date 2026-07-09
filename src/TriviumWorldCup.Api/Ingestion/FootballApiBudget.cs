namespace TriviumWorldCup.Api.Ingestion;

/// <summary>
/// Paces and caps Football API usage so the ingestion pipeline stays within the API-Football
/// <b>free plan</b> limit of 100 requests/day. On the free plan there is at most one match per
/// day, so "100 requests/day" is effectively "100 requests per match" — this class enforces
/// exactly that.
///
/// Two independent guards, both active only when <see cref="Enabled"/> is true:
///   1. PACING — <see cref="ShouldPollThisCycle"/> lets the ingestion job touch the API at most
///      once every <see cref="MinPollInterval"/> instead of on every 30-second Quartz tick. The
///      interval is sized so the ~100-call budget is spread across the <i>entire</i> match window
///      — kick-off, through 90 minutes, extra time, and the penalty shootout — so calls are still
///      available when the penalties finish. (The match's live window itself stays open through
///      PenaltyShootout status; see <see cref="ResultIngestionJob"/> step 1.)
///   2. HARD CAP — <see cref="TryConsumeCall"/> refuses the (<see cref="MaxCallsPerDay"/>+1)-th
///      call within a UTC day. The counter resets at 00:00 UTC, matching API-Football's own quota
///      reset, and is a backstop only: with the default interval the job never approaches it.
///
/// When <see cref="Enabled"/> is false the class is a no-op: the job polls every 30 seconds and no
/// cap applies — the original "pro plan" behaviour. Flip it at runtime via
/// <c>POST /admin/ingestion/budget</c>, or seed the default from <c>Ingestion:Budget</c> config.
///
/// Registered as a singleton. The ingestion job runs single-threaded
/// (DisallowConcurrentExecution), but the HTTP client may consume calls concurrently, so all
/// mutable state is guarded by a lock.
/// </summary>
public sealed class FootballApiBudget
{
    private readonly object _gate = new();
    private DateOnly _counterDayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
    private int _callsToday;
    private DateTimeOffset? _lastActivePollUtc;

    public FootballApiBudget(bool enabled, int maxCallsPerDay, TimeSpan minPollInterval)
    {
        Enabled = enabled;
        MaxCallsPerDay = maxCallsPerDay;
        MinPollInterval = minPollInterval;
    }

    /// <summary>A disabled, default-limits budget — used where no real budget is injected (tests).</summary>
    public static FootballApiBudget Disabled => new(enabled: false, maxCallsPerDay: 100, TimeSpan.FromSeconds(240));

    /// <summary>When false, pacing and the daily cap are both bypassed (original pro-plan behaviour).</summary>
    public bool Enabled { get; set; }

    /// <summary>Maximum API calls allowed per UTC day when <see cref="Enabled"/>. Free plan = 100.</summary>
    public int MaxCallsPerDay { get; set; }

    /// <summary>Minimum wall-clock gap between API-touching poll cycles when <see cref="Enabled"/>.</summary>
    public TimeSpan MinPollInterval { get; set; }

    /// <summary>Calls consumed so far in the current UTC day (auto-resets at 00:00 UTC).</summary>
    public int CallsToday
    {
        get { lock (_gate) { RollDayIfNeeded(DateTimeOffset.UtcNow); return _callsToday; } }
    }

    /// <summary>Timestamp of the last poll cycle that was cleared to touch the API, or null.</summary>
    public DateTimeOffset? LastActivePollUtc
    {
        get { lock (_gate) return _lastActivePollUtc; }
    }

    /// <summary>
    /// True when the job may make API calls this cycle. Always true in disabled ("pro plan") mode.
    /// In budget mode, true only when enough time has elapsed since the last API-touching poll AND
    /// the daily cap has not been reached. Does not mutate state — call <see cref="MarkActivePoll"/>
    /// once the job actually commits to polling.
    /// </summary>
    public bool ShouldPollThisCycle(DateTimeOffset now)
    {
        if (!Enabled) return true;
        lock (_gate)
        {
            // Day rollover is always tracked against real UTC time (consistent with
            // TryConsumeCall); the passed-in `now` drives only the interval comparison.
            RollDayIfNeeded(DateTimeOffset.UtcNow);
            if (_callsToday >= MaxCallsPerDay) return false;
            return _lastActivePollUtc is null || now - _lastActivePollUtc >= MinPollInterval;
        }
    }

    /// <summary>Records that the job started an API-touching poll at <paramref name="now"/>.</summary>
    public void MarkActivePoll(DateTimeOffset now)
    {
        lock (_gate) _lastActivePollUtc = now;
    }

    /// <summary>
    /// Records one API call against the daily budget. Returns false when the call would exceed
    /// <see cref="MaxCallsPerDay"/> (budget mode only) — the caller must then abort the request.
    /// In disabled mode it always returns true, still incrementing the counter so usage remains
    /// observable via <see cref="CallsToday"/>.
    /// </summary>
    public bool TryConsumeCall()
    {
        lock (_gate)
        {
            RollDayIfNeeded(DateTimeOffset.UtcNow);
            if (Enabled && _callsToday >= MaxCallsPerDay) return false;
            _callsToday++;
            return true;
        }
    }

    private void RollDayIfNeeded(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (today == _counterDayUtc) return;
        _counterDayUtc = today;
        _callsToday = 0;
    }
}
