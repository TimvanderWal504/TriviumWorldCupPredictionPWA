using TriviumWorldCup.Api.Ingestion;

namespace TriviumWorldCup.Api.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="FootballApiBudget"/> — the free-plan pacer/cap that keeps
/// Football API usage under 100 calls/day and spreads it across a full match (through penalties).
/// Pure in-memory logic; no database or HTTP.
/// </summary>
public class FootballApiBudgetTests
{
    private static FootballApiBudget Enabled(int cap = 100, int intervalSeconds = 240) =>
        new(enabled: true, maxCallsPerDay: cap, TimeSpan.FromSeconds(intervalSeconds));

    // ── Disabled mode = original pro-plan behaviour (no pacing, no cap) ─────────

    [Fact]
    public void Disabled_ShouldPollThisCycle_AlwaysTrue()
    {
        var budget = new FootballApiBudget(enabled: false, maxCallsPerDay: 100, TimeSpan.FromSeconds(240));
        var now = DateTimeOffset.UtcNow;

        Assert.True(budget.ShouldPollThisCycle(now));
        budget.MarkActivePoll(now);
        // Immediately again, no interval respected when disabled.
        Assert.True(budget.ShouldPollThisCycle(now));
    }

    [Fact]
    public void Disabled_TryConsumeCall_NeverCapped()
    {
        var budget = new FootballApiBudget(enabled: false, maxCallsPerDay: 5, TimeSpan.FromSeconds(240));

        for (var i = 0; i < 20; i++)
            Assert.True(budget.TryConsumeCall());
    }

    // ── Pacing ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Enabled_FirstCycleAllowed_ThenPacedUntilIntervalElapses()
    {
        var budget = Enabled(intervalSeconds: 240);
        var t0 = new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        Assert.True(budget.ShouldPollThisCycle(t0));      // first ever poll — allowed
        budget.MarkActivePoll(t0);

        Assert.False(budget.ShouldPollThisCycle(t0.AddSeconds(30)));   // too soon
        Assert.False(budget.ShouldPollThisCycle(t0.AddSeconds(239)));  // still too soon
        Assert.True(budget.ShouldPollThisCycle(t0.AddSeconds(240)));   // interval elapsed
    }

    [Fact]
    public void Enabled_SpreadsRoughlyOneHundredCallsAcrossAFullMatchWindow()
    {
        // Sanity check the default sizing: at 240s pacing with ~2 calls per active poll,
        // a ~185-minute window (kick-off − 30 min through extra time + penalties) stays
        // comfortably under the 100-call cap, so calls remain available when penalties end.
        var budget = Enabled(cap: 100, intervalSeconds: 240);
        var start = new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        var calls = 0;
        for (var t = start; t <= start.AddMinutes(185); t = t.AddSeconds(30))
        {
            if (!budget.ShouldPollThisCycle(t)) continue;
            budget.MarkActivePoll(t);
            // Simulate the two API calls a live poll makes (fixtures-by-date + events).
            if (budget.TryConsumeCall()) calls++;
            if (budget.TryConsumeCall()) calls++;
        }

        Assert.True(calls <= 100, $"expected <= 100 calls, got {calls}");
        Assert.True(calls >= 80, $"expected the budget to be well used (>= 80), got {calls}");
    }

    // ── Hard cap ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Enabled_TryConsumeCall_StopsAtCap()
    {
        var budget = Enabled(cap: 3);

        Assert.True(budget.TryConsumeCall());
        Assert.True(budget.TryConsumeCall());
        Assert.True(budget.TryConsumeCall());
        Assert.False(budget.TryConsumeCall());   // 4th refused
        Assert.Equal(3, budget.CallsToday);
    }

    [Fact]
    public void Enabled_ShouldPollThisCycle_FalseOnceCapReached()
    {
        var budget = Enabled(cap: 2, intervalSeconds: 1);
        var t0 = new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        budget.TryConsumeCall();
        budget.TryConsumeCall();   // cap reached

        Assert.False(budget.ShouldPollThisCycle(t0.AddHours(1)));
    }

    // ── Call counting ────────────────────────────────────────────────────────────

    [Fact]
    public void CallsToday_TracksConsumedCalls()
    {
        var budget = Enabled(cap: 100);

        Assert.Equal(0, budget.CallsToday);
        budget.TryConsumeCall();
        budget.TryConsumeCall();
        Assert.Equal(2, budget.CallsToday);
    }
}
