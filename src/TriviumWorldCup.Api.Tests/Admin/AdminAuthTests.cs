using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Mock;

namespace TriviumWorldCup.Api.Tests.Admin;

/// <summary>
/// Unit tests for the admin auth guard used by all admin endpoints.
/// Tests the pure IsInRole("admin") check that every admin handler performs.
/// TWC-16 acceptance criteria: only admins can reach the screen and mutate data.
/// </summary>
public class AdminAuthTests
{
    // ── IsInRole("admin") helper ─────────────────────────────────────────────

    /// <summary>
    /// Simulates the per-endpoint guard: user.IsInRole("admin").
    /// </summary>
    private static bool AdminGuard(AppUser user) => user.IsInRole("admin");

    // ── Non-admin users are blocked ──────────────────────────────────────────

    [Fact]
    public void NonAdminUser_FailsAdminGuard()
    {
        var alice = new AppUser("a1b2c3d4-0001-0001-0001-000000000001", "Alice", ["user"]);
        Assert.False(AdminGuard(alice));
    }

    [Fact]
    public void Anonymous_FailsAdminGuard()
    {
        Assert.False(AdminGuard(AppUser.Anonymous));
    }

    [Fact]
    public void UserWithNoRoles_FailsAdminGuard()
    {
        var user = new AppUser("some-id", "NoRole", []);
        Assert.False(AdminGuard(user));
    }

    [Theory]
    [InlineData("a1b2c3d4-0001-0001-0001-000000000001", "Alice",   new[] { "user" })]
    [InlineData("a1b2c3d4-0002-0002-0002-000000000002", "Bob",     new[] { "user" })]
    [InlineData("a1b2c3d4-0003-0003-0003-000000000003", "Charlie", new[] { "user" })]
    [InlineData("a1b2c3d4-0005-0005-0005-000000000005", "Evan",    new[] { "user" })]
    public void AllNonAdminSeedUsers_FailAdminGuard(string userId, string displayName, string[] roles)
    {
        var user = new AppUser(userId, displayName, roles);
        Assert.False(AdminGuard(user));
    }

    // ── Admin user passes ────────────────────────────────────────────────────

    [Fact]
    public void DianaAdmin_PassesAdminGuard()
    {
        // Diana is the only seeded admin user.
        var diana = MockUsers.FindById("a1b2c3d4-0004-0004-0004-000000000004");
        Assert.NotNull(diana);
        Assert.True(AdminGuard(diana));
    }

    [Fact]
    public void AdminRoleIsCaseInsensitive_UpperCase_Passes()
    {
        var user = new AppUser("id-admin", "AdminUser", ["ADMIN"]);
        Assert.True(AdminGuard(user));
    }

    [Fact]
    public void AdminRoleIsCaseInsensitive_MixedCase_Passes()
    {
        var user = new AppUser("id-admin", "AdminUser", ["Admin"]);
        Assert.True(AdminGuard(user));
    }

    [Fact]
    public void UserWithBothRoles_PassesAdminGuard()
    {
        var user = new AppUser("id-dual", "DualRole", ["user", "admin"]);
        Assert.True(AdminGuard(user));
    }

    // ── MockUsers: exactly one admin ─────────────────────────────────────────

    [Fact]
    public void ExactlyOneSeedUser_IsAdmin()
    {
        var admins = MockUsers.All.Where(u => u.IsInRole("admin")).ToList();
        Assert.Single(admins);
        Assert.Equal("a1b2c3d4-0004-0004-0004-000000000004", admins[0].UserId);
        Assert.Equal("Diana", admins[0].DisplayName);
    }

    [Fact]
    public void AllOtherSeedUsers_AreNotAdmin()
    {
        var nonAdmins = MockUsers.All.Where(u => !u.IsInRole("admin")).ToList();
        Assert.Equal(4, nonAdmins.Count);
        Assert.DoesNotContain(nonAdmins, u => u.DisplayName == "Diana");
    }

    // ── IngestionStatusStore defaults ────────────────────────────────────────

    [Fact]
    public void IngestionStatusStore_DefaultsAreNull_And_ZeroCounts()
    {
        var store = new TriviumWorldCup.Api.Admin.IngestionStatusStore();

        Assert.Null(store.LastSuccessfulPoll);
        Assert.Null(store.LastAttemptedPoll);
        Assert.Null(store.LastError);
        Assert.Equal(0, store.TotalPollCount);
        Assert.Equal(0, store.ErrorCount);
    }

    [Fact]
    public void IngestionStatusStore_CanBeUpdated()
    {
        var store = new TriviumWorldCup.Api.Admin.IngestionStatusStore();
        var now = DateTimeOffset.UtcNow;

        store.LastAttemptedPoll = now;
        store.TotalPollCount    = 5;
        store.LastSuccessfulPoll = now;
        store.ErrorCount         = 2;
        store.LastError          = "Connection timeout";

        Assert.Equal(now, store.LastAttemptedPoll);
        Assert.Equal(now, store.LastSuccessfulPoll);
        Assert.Equal(5, store.TotalPollCount);
        Assert.Equal(2, store.ErrorCount);
        Assert.Equal("Connection timeout", store.LastError);
    }

    // ── ResultOverride model ─────────────────────────────────────────────────

    [Fact]
    public void ResultOverride_CanBeCreated_WithExpectedFields()
    {
        var id  = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var overrideRecord = new TriviumWorldCup.Api.Domain.ResultOverride
        {
            Id               = id,
            AdminUserId      = "a1b2c3d4-0004-0004-0004-000000000004",
            AdminDisplayName = "Diana",
            OverriddenAt     = now,
            TargetType       = "fixture",
            TargetId         = "42",
            Description      = "Set result 2-1",
        };

        Assert.Equal(id, overrideRecord.Id);
        Assert.Equal("Diana", overrideRecord.AdminDisplayName);
        Assert.Equal("fixture", overrideRecord.TargetType);
        Assert.Equal("42", overrideRecord.TargetId);
        Assert.Equal("Set result 2-1", overrideRecord.Description);
    }

    [Theory]
    [InlineData("fixture")]
    [InlineData("goalevent")]
    public void ResultOverride_TargetType_AcceptsExpectedValues(string targetType)
    {
        var overrideRecord = new TriviumWorldCup.Api.Domain.ResultOverride
        {
            Id               = Guid.NewGuid(),
            AdminUserId      = "some-id",
            AdminDisplayName = "Admin",
            OverriddenAt     = DateTimeOffset.UtcNow,
            TargetType       = targetType,
            TargetId         = "1",
            Description      = "Test",
        };

        Assert.Equal(targetType, overrideRecord.TargetType);
    }
}
