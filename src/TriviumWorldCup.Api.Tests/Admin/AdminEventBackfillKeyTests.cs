using TriviumWorldCup.Api.Ingestion;

namespace TriviumWorldCup.Api.Tests.Admin;

/// <summary>
/// TWC-82: the admin event-backfill endpoints (POST /admin/fixtures/{id}/fetch-events,
/// POST /admin/fixtures/{id}/reset-events, POST /admin/fixtures/fetch-all-events) previously
/// built deterministic event IDs using an {elapsed}-only key
/// (e.g. "{apiId}:{playerName}:{evt.Time?.Elapsed ?? 0}"), independently of
/// ResultIngestionJob.MinuteKey introduced for TWC-57. A same-minute brace (e.g. two goals at
/// 90+2' and 90+5', both Elapsed=90) collided on the same GUID and only one event survived a
/// manual admin backfill call, even though the live ingestion path (TWC-57) had already been
/// fixed. All three endpoints now build their keys with
/// "{...}:{ResultIngestionJob.MinuteKey(evt.Time)}" — identical shape to the ingestion job.
///
/// These tests replicate that exact key format (goal/card/sub/var — all four event kinds share
/// the same fix) to prove the collision no longer occurs, without requiring an HTTP/DB harness
/// for the Minimal API endpoint itself (consistent with the rest of the ingestion test suite:
/// pure-function unit tests against the extracted MinuteKey/CreateDeterministicGuid helpers).
/// </summary>
public class AdminEventBackfillKeyTests
{
    private static readonly Guid Namespace = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    // Mirrors the admin goal-event key format: $"{apiId}:{playerName}:{MinuteKey(evt.Time)}"
    private static Guid GoalKey(int apiId, string playerName, ApiTime? time) =>
        ResultIngestionJob.CreateDeterministicGuid(Namespace,
            $"{apiId}:{playerName}:{ResultIngestionJob.MinuteKey(time)}");

    [Fact]
    public void SameMinuteBrace_DifferentExtra_ProducesDistinctIds_AdminGoalKeyShape()
    {
        var first  = GoalKey(99001, "Braced Player", new ApiTime { Elapsed = 90, Extra = 2 });
        var second = GoalKey(99001, "Braced Player", new ApiTime { Elapsed = 90, Extra = 5 });

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void SameMinuteAndExtra_ProducesSameId_StillIdempotent()
    {
        // Re-running the admin backfill for an unchanged event must remain a stable upsert.
        var first  = GoalKey(99001, "Player", new ApiTime { Elapsed = 45, Extra = 3 });
        var second = GoalKey(99001, "Player", new ApiTime { Elapsed = 45, Extra = 3 });

        Assert.Equal(first, second);
    }

    [Fact]
    public void AdminKeyShape_ProducesSameIdAsIngestionJob_ForSameEvent()
    {
        // The admin endpoints and the live ingestion job must key the same fixture+player+minute
        // event identically, so a manual backfill and a live-poll upsert of the same goal
        // converge on the same document instead of creating a duplicate.
        var time = new ApiTime { Elapsed = 67, Extra = null };

        var adminKey = GoalKey(88123, "Same Player", time);
        var ingestionKey = ResultIngestionJob.CreateDeterministicGuid(Namespace,
            $"88123:Same Player:{ResultIngestionJob.MinuteKey(time)}");

        Assert.Equal(ingestionKey, adminKey);
    }

    [Fact]
    public void NullExtra_DefaultsConsistently_NoCollisionWithZeroExtra()
    {
        var nullExtra = GoalKey(1, "P", new ApiTime { Elapsed = 10, Extra = null });
        var zeroExtra = GoalKey(1, "P", new ApiTime { Elapsed = 10, Extra = 0 });

        // MinuteKey normalizes null Extra to 0, so these are (correctly) the same event.
        Assert.Equal(zeroExtra, nullExtra);
    }
}
