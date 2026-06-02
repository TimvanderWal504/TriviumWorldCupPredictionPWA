using Microsoft.AspNetCore.Http;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Mock;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Tests for the mock identity provider: user resolution, unknown IDs,
/// and unauthenticated (no cookie) requests.
/// </summary>
public class MockIdentityProviderTests
{
    private readonly MockIdentityProvider _sut = new();

    [Fact]
    public async Task GetCurrentUserAsync_WithValidCookie_ReturnsMatchingUser()
    {
        // Arrange
        var alice = MockUsers.All.First(u => u.DisplayName == "Alice");
        var context = BuildContextWithCookie(alice.UserId);

        // Act
        var user = await _sut.GetCurrentUserAsync(context);

        // Assert
        Assert.True(user.IsAuthenticated);
        Assert.Equal(alice.UserId, user.UserId);
        Assert.Equal("Alice", user.DisplayName);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithNoCookie_ReturnsAnonymous()
    {
        var context = new DefaultHttpContext();

        var user = await _sut.GetCurrentUserAsync(context);

        Assert.False(user.IsAuthenticated);
        Assert.Same(AppUser.Anonymous, user);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithUnknownUserId_ReturnsAnonymous()
    {
        var context = BuildContextWithCookie("00000000-dead-beef-dead-000000000000");

        var user = await _sut.GetCurrentUserAsync(context);

        Assert.False(user.IsAuthenticated);
        Assert.Same(AppUser.Anonymous, user);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithEmptyCookieValue_ReturnsAnonymous()
    {
        var context = BuildContextWithCookie("");

        var user = await _sut.GetCurrentUserAsync(context);

        Assert.False(user.IsAuthenticated);
    }

    [Theory]
    [InlineData("Alice")]
    [InlineData("Bob")]
    [InlineData("Charlie")]
    [InlineData("Diana")]
    [InlineData("Evan")]
    public async Task GetCurrentUserAsync_CanResolveAllSeededUsers(string displayName)
    {
        var seededUser = MockUsers.All.First(u => u.DisplayName == displayName);
        var context = BuildContextWithCookie(seededUser.UserId);

        var user = await _sut.GetCurrentUserAsync(context);

        Assert.True(user.IsAuthenticated);
        Assert.Equal(displayName, user.DisplayName);
    }

    [Fact]
    public async Task GetCurrentUserAsync_UserId_IsCaseInsensitive()
    {
        var alice = MockUsers.All.First(u => u.DisplayName == "Alice");
        // Cookie value in upper-case should still resolve
        var context = BuildContextWithCookie(alice.UserId.ToUpperInvariant());

        var user = await _sut.GetCurrentUserAsync(context);

        Assert.True(user.IsAuthenticated);
        Assert.Equal("Alice", user.DisplayName);
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
