namespace TriviumWorldCup.Api.Auth.Mock;

/// <summary>
/// Seeded demo users for the mock identity provider.
/// GUIDs are stable — do not change them after seeding.
/// </summary>
public static class MockUsers
{
    public static readonly IReadOnlyList<AppUser> All = [
        new AppUser("a1b2c3d4-0001-0001-0001-000000000001", "Alice",   ["user"]),
        new AppUser("a1b2c3d4-0002-0002-0002-000000000002", "Bob",     ["user"]),
        new AppUser("a1b2c3d4-0003-0003-0003-000000000003", "Charlie", ["user"]),
        new AppUser("a1b2c3d4-0004-0004-0004-000000000004", "Diana",   ["user", "admin"]),
        new AppUser("a1b2c3d4-0005-0005-0005-000000000005", "Evan",    ["user"]),
    ];

    private static readonly Dictionary<string, AppUser> _byId =
        All.ToDictionary(u => u.UserId, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Looks up a user by their stable GUID. Returns null for unknown IDs.
    /// </summary>
    public static AppUser? FindById(string userId) =>
        _byId.TryGetValue(userId, out var user) ? user : null;
}
