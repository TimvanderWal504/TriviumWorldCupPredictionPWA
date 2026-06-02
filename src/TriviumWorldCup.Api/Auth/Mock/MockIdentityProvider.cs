namespace TriviumWorldCup.Api.Auth.Mock;

/// <summary>
/// Dev/demo-only identity provider.
/// Reads the active user from a signed cookie set by the mock auth endpoints.
/// Must never be active in Production — enforced at startup by
/// <see cref="AuthServiceExtensions.AddAuthAbstraction"/>.
/// </summary>
public sealed class MockIdentityProvider : IIdentityProvider
{
    public const string CookieName = "twc_mock_user";

    public Task<AppUser> GetCurrentUserAsync(HttpContext context, CancellationToken ct = default)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var userId)
            && !string.IsNullOrWhiteSpace(userId))
        {
            var user = MockUsers.FindById(userId);
            if (user is not null)
                return Task.FromResult(user);
        }

        return Task.FromResult(AppUser.Anonymous);
    }
}
