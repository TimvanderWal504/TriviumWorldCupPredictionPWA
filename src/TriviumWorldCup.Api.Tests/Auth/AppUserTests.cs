using TriviumWorldCup.Api.Auth;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Tests for the AppUser record — authenticated state, role checks, and Anonymous singleton.
/// </summary>
public class AppUserTests
{
    [Fact]
    public void Anonymous_IsNotAuthenticated()
    {
        Assert.False(AppUser.Anonymous.IsAuthenticated);
        Assert.Empty(AppUser.Anonymous.UserId);
        Assert.Empty(AppUser.Anonymous.Roles);
    }

    [Fact]
    public void AppUser_WithUserId_IsAuthenticated()
    {
        var user = new AppUser("user-1", "Alice", ["user"]);
        Assert.True(user.IsAuthenticated);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("ADMIN", true)]   // case-insensitive
    [InlineData("user", false)]
    [InlineData("superuser", false)]
    public void IsInRole_ReturnsCorrectly(string role, bool expected)
    {
        var user = new AppUser("id-1", "Alice", ["admin"]);
        Assert.Equal(expected, user.IsInRole(role));
    }

    [Fact]
    public void AppUser_WithEmptyUserId_IsNotAuthenticated()
    {
        var user = new AppUser(string.Empty, "Ghost", []);
        Assert.False(user.IsAuthenticated);
    }
}
