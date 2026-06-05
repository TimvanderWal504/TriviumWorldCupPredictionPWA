using Marten;

namespace TriviumWorldCup.Api.Auth.Link;

/// <summary>
/// Identity provider for the admin-managed link auth system.
/// Reads the session cookie set by /auth/link/login and resolves
/// the matching InviteUser from the database.
/// </summary>
public sealed class LinkIdentityProvider : IIdentityProvider
{
    public const string CookieName = "twc_link_session";

    private readonly IDocumentStore _store;

    public LinkIdentityProvider(IDocumentStore store) => _store = store;

    public async Task<AppUser> GetCurrentUserAsync(HttpContext context, CancellationToken ct = default)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var userId)
            || string.IsNullOrWhiteSpace(userId))
            return AppUser.Anonymous;

        await using var session = _store.LightweightSession();
        var user = await session.LoadAsync<InviteUser>(userId, ct);
        if (user is null)
            return AppUser.Anonymous;

        return new AppUser(user.Id, user.DisplayName, user.Roles);
    }
}
