using Microsoft.AspNetCore.Http;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Mock;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Tests for CurrentUserMiddleware — verifies the resolved user is placed into
/// HttpContext.Items and is retrievable via GetAppUser().
/// </summary>
public class CurrentUserMiddlewareTests
{
    [Fact]
    public async Task Middleware_PlacesResolvedUserInHttpContextItems()
    {
        // Arrange
        var alice = MockUsers.All.First(u => u.DisplayName == "Alice");
        var context = BuildContextWithCookie(alice.UserId);

        AppUser? captured = null;
        var middleware = new CurrentUserMiddleware(ctx =>
        {
            captured = ctx.GetAppUser();
            return Task.CompletedTask;
        });

        var provider = new MockIdentityProvider();

        // Act
        await middleware.InvokeAsync(context, provider);

        // Assert
        Assert.NotNull(captured);
        Assert.True(captured.IsAuthenticated);
        Assert.Equal("Alice", captured.DisplayName);
    }

    [Fact]
    public async Task Middleware_WithNoCookie_PlacesAnonymousInContext()
    {
        var context = new DefaultHttpContext();
        AppUser? captured = null;

        var middleware = new CurrentUserMiddleware(ctx =>
        {
            captured = ctx.GetAppUser();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, new MockIdentityProvider());

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

    private static HttpContext BuildContextWithCookie(string userId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{MockIdentityProvider.CookieName}={Uri.EscapeDataString(userId)}";
        return context;
    }
}
