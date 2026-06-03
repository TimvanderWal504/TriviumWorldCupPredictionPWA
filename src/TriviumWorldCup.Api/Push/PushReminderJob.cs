using Marten;
using Quartz;
using WebPush;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Push;

/// <summary>
/// Quartz job that runs every 30 minutes and sends Web Push reminders to members
/// who have an active push subscription but have not yet predicted a group fixture
/// that kicks off within the next 2 hours.
///
/// 410 Gone responses from the push service indicate the subscription has been revoked
/// by the browser — those documents are deleted from Marten.
/// </summary>
[DisallowConcurrentExecution]
public class PushReminderJob(
    IDocumentStore store,
    WebPushClient webPushClient,
    IConfiguration config,
    ILogger<PushReminderJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var vapidPublicKey  = config["Push:VapidPublicKey"]  ?? string.Empty;
        var vapidPrivateKey = config["Push:VapidPrivateKey"] ?? string.Empty;
        var vapidSubject    = config["Push:VapidSubject"]    ?? string.Empty;

        if (string.IsNullOrWhiteSpace(vapidPublicKey) ||
            string.IsNullOrWhiteSpace(vapidPrivateKey) ||
            string.IsNullOrWhiteSpace(vapidSubject))
        {
            logger.LogDebug("PushReminderJob: VAPID keys not configured — skipping run");
            return;
        }

        var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);

        var now       = DateTimeOffset.UtcNow;
        var windowEnd = now.AddHours(2);

        await using var session = store.LightweightSession();

        // ── 1. Find upcoming scheduled fixtures (kickoff in the next 2 hours) ──
        var upcomingFixtures = await session.Query<Fixture>()
            .Where(f => f.Status == MatchStatus.Scheduled
                     && f.KickoffUtc > now
                     && f.KickoffUtc <= windowEnd)
            .ToListAsync(ct);

        if (upcomingFixtures.Count == 0)
        {
            logger.LogDebug("PushReminderJob: no upcoming fixtures in the next 2 hours — skipping");
            return;
        }

        logger.LogDebug("PushReminderJob: {Count} fixture(s) upcoming in next 2 hours", upcomingFixtures.Count);

        // ── 2. Load all active push subscriptions ─────────────────────────────
        var allSubscriptions = await session.Query<Domain.PushSubscription>()
            .ToListAsync(ct);

        if (allSubscriptions.Count == 0)
        {
            logger.LogDebug("PushReminderJob: no push subscriptions — skipping");
            return;
        }

        // Index subscriptions by UserId (one user may have multiple devices)
        var subscriptionsByUser = allSubscriptions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── 3. For each fixture, find subscribers who have not predicted ───────
        var expiredEndpoints = new List<string>();
        var sentCount        = 0;
        var skippedCount     = 0;

        foreach (var fixture in upcomingFixtures)
        {
            var minutesUntilKickoff = (int)(fixture.KickoffUtc - now).TotalMinutes;

            foreach (var (userId, subs) in subscriptionsByUser)
            {
                // Check if this user has already predicted this fixture
                var predictionId = $"{userId}_{fixture.Id}";
                var hasPrediction = await session.LoadAsync<GroupPrediction>(predictionId, ct) is not null;

                if (hasPrediction)
                {
                    skippedCount++;
                    continue;
                }

                // Build notification payload
                var body = $"{fixture.HomeTeamId} vs {fixture.AwayTeamId} kicks off in " +
                           $"{minutesUntilKickoff} minutes — you haven't predicted yet!";

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    title = "Prediction reminder",
                    body,
                });

                // Send to all devices for this user
                foreach (var sub in subs)
                {
                    var webPushSub = new WebPush.PushSubscription
                    {
                        Endpoint = sub.Endpoint,
                        P256DH   = sub.P256dh,
                        Auth     = sub.Auth,
                    };

                    try
                    {
                        await webPushClient.SendNotificationAsync(webPushSub, payload, vapidDetails);
                        sentCount++;
                        logger.LogInformation(
                            "PushReminderJob: sent reminder to user {UserId} for fixture {FixtureId}",
                            userId, fixture.Id);
                    }
                    catch (WebPushException ex) when ((int)ex.StatusCode == 410)
                    {
                        // 410 Gone — browser has unsubscribed; remove from Marten
                        logger.LogInformation(
                            "PushReminderJob: subscription {Endpoint} returned 410 Gone — removing",
                            sub.Endpoint);
                        expiredEndpoints.Add(sub.Endpoint);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "PushReminderJob: failed to send to endpoint {Endpoint} for user {UserId}",
                            sub.Endpoint, userId);
                    }
                }
            }
        }

        // ── 4. Remove expired subscriptions ───────────────────────────────────
        if (expiredEndpoints.Count > 0)
        {
            var toDelete = await session.Query<Domain.PushSubscription>()
                .Where(s => s.Endpoint.IsOneOf(expiredEndpoints))
                .ToListAsync(ct);

            foreach (var sub in toDelete)
                session.Delete(sub);

            await session.SaveChangesAsync(ct);

            logger.LogInformation(
                "PushReminderJob: removed {Count} expired subscription(s)", toDelete.Count);
        }

        logger.LogInformation(
            "PushReminderJob: completed — sent={Sent}, skipped={Skipped}, expired-removed={Expired}",
            sentCount, skippedCount, expiredEndpoints.Count);
    }
}
