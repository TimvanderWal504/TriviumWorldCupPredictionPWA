using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Push;

/// <summary>
/// Push notification subscription endpoints.
/// POST /push/subscribe  — opt in (auth required)
/// DELETE /push/subscribe — opt out (auth required)
/// GET /push/vapid-public-key — returns the VAPID public key so the browser can subscribe (no auth)
/// </summary>
public static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/push").WithTags("push");

        // GET /push/vapid-public-key — unauthenticated; browser needs this to call PushManager.subscribe
        group.MapGet("/vapid-public-key", (IConfiguration config) =>
        {
            var publicKey = (config["Push:VapidPublicKey"] ?? string.Empty).Trim();
            return Results.Ok(new { publicKey });
        })
        .WithName("GetVapidPublicKey")
        .WithSummary("Returns the VAPID public key for browser push subscription.");

        // POST /push/subscribe — store or update a push subscription for the calling user
        group.MapPost("/subscribe", async (
            HttpContext context,
            [FromBody] SubscribeRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Endpoint) ||
                string.IsNullOrWhiteSpace(request.P256dh) ||
                string.IsNullOrWhiteSpace(request.Auth))
                return Results.BadRequest(new { error = "endpoint, p256dh, and auth are required." });

            // Upsert by Endpoint: if this device endpoint already exists (for any user), replace it
            var existing = await session.Query<PushSubscription>()
                .Where(s => s.Endpoint == request.Endpoint)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                existing.UserId = user.UserId;
                existing.P256dh = request.P256dh;
                existing.Auth = request.Auth;
                session.Store(existing);
            }
            else
            {
                var subscription = new PushSubscription
                {
                    Id        = Guid.NewGuid(),
                    UserId    = user.UserId,
                    Endpoint  = request.Endpoint,
                    P256dh    = request.P256dh,
                    Auth      = request.Auth,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                session.Store(subscription);
            }

            await session.SaveChangesAsync(ct);
            return Results.Ok();
        })
        .WithName("SubscribePush")
        .WithSummary("Stores or updates a Web Push subscription for the current user.");

        // DELETE /push/subscribe — remove a subscription by endpoint (idempotent)
        group.MapDelete("/subscribe", async (
            HttpContext context,
            [FromBody] UnsubscribeRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Endpoint))
                return Results.BadRequest(new { error = "endpoint is required." });

            var existing = await session.Query<PushSubscription>()
                .Where(s => s.Endpoint == request.Endpoint && s.UserId == user.UserId)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
            {
                session.Delete(existing);
                await session.SaveChangesAsync(ct);
            }

            // Idempotent: 204 whether or not a subscription was found
            return Results.NoContent();
        })
        .WithName("UnsubscribePush")
        .WithSummary("Removes a Web Push subscription for the current user (idempotent).");

        return routes;
    }
}

/// <summary>Request body for POST /push/subscribe.</summary>
public sealed record SubscribeRequest(string Endpoint, string P256dh, string Auth);

/// <summary>Request body for DELETE /push/subscribe.</summary>
public sealed record UnsubscribeRequest(string Endpoint);
