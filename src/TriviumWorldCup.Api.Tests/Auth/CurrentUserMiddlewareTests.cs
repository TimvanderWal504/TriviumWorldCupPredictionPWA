using Microsoft.AspNetCore.Http;
using TriviumWorldCup.Api.Auth;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Tests for CurrentUserMiddleware — verifies the resolved user is placed into
/// HttpContext.Items and is retrievable via GetAppUser().
/// Uses an inline stub provider so the tests are not tied to any concrete auth provider.
/// </summary>
public class CurrentUserMiddlewareTests
{
    [Fact]
    public async Task Middleware_PlacesResolvedUserInHttpContextItems()
    {
        var alice = new AppUser("test-alice-001", "Alice", ["user"]);
        var context = new DefaultHttpContext();
        AppUser? captured = null;

        var middleware = new CurrentUserMiddleware(ctx =>
        {
            captured = ctx.GetAppUser();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new StubIdentityProvider(alice));

        Assert.NotNull(captured);
        Assert.True(captured.IsAuthenticated);
        Assert.Equal("Alice", captured.DisplayName);
    }

    [Fact]
    public async Task Middleware_WithAnonymousProvider_PlacesAnonymousInContext()
    {
        var context = new DefaultHttpContext();
        AppUser? captured = null;

        var middleware = new CurrentUserMiddleware(ctx =>
        {
            captured = ctx.GetAppUser();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new StubIdentityProvider(AppUser.Anonymous));

        Assert.NotNull(captured);
        Assert.False(captured.IsAuthenticated);
    }

    [Fact]
    public void GetAppUser_WhenItemNotSet_ReturnsAnonymous()
    {
        var context = new DefaultHttpContext();
        // No middleware ran — Items is empty
        var user = context.GetAppUser();
        Assert.Same(AppUser.Anonymous, user);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed class StubIdentityProvider(AppUser user) : IIdentityProvider
    {
        public Task<AppUser> GetCurrentUserAsync(HttpContext context, CancellationToken ct = default)
            => Task.FromResult(user);
    }
}
