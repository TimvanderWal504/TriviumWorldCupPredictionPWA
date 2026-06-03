namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// A Web Push subscription for a single device of a single member.
/// Stored as a Marten document; unique per Endpoint (which is device/browser-scoped).
/// </summary>
public class PushSubscription
{
    /// <summary>Marten document id — Guid generated on creation.</summary>
    public Guid Id { get; set; }

    /// <summary>The authenticated user who owns this subscription.</summary>
    public string UserId { get; set; } = default!;

    /// <summary>The push service endpoint URL — unique per browser/device registration.</summary>
    public string Endpoint { get; set; } = default!;

    /// <summary>Browser-side ECDH public key (base64url-encoded) — passed as-is to WebPush library.</summary>
    public string P256dh { get; set; } = default!;

    /// <summary>Auth secret (base64url-encoded) — passed as-is to WebPush library.</summary>
    public string Auth { get; set; } = default!;

    /// <summary>When this subscription was first stored.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
