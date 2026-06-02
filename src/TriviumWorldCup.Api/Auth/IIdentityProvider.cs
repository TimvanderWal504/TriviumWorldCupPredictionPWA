namespace TriviumWorldCup.Api.Auth;

/// <summary>
/// Resolves the current authenticated user from the HTTP context.
/// Implementations: MockIdentityProvider (dev/demo), EntraIdentityProvider (TWC-20).
/// Feature code depends only on this interface — never on a concrete provider.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>
    /// Returns the authenticated user for the current request, or
    /// <see cref="AppUser.Anonymous"/> if the request is unauthenticated.
    /// </summary>
    Task<AppUser> GetCurrentUserAsync(HttpContext context, CancellationToken ct = default);
}
