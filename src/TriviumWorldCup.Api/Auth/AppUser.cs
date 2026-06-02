namespace TriviumWorldCup.Api.Auth;

/// <summary>
/// The resolved, authenticated identity passed to feature handlers.
/// Provider-agnostic — never tied to a concrete auth mechanism.
/// </summary>
/// <param name="UserId">Stable, provider-issued unique identifier.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="Roles">Role claims carried by this user.</param>
public sealed record AppUser(
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles)
{
    /// <summary>Convenience singleton for unauthenticated / anonymous requests.</summary>
    public static readonly AppUser Anonymous = new(
        UserId: string.Empty,
        DisplayName: string.Empty,
        Roles: []);

    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);

    public bool IsInRole(string role) =>
        Roles.Any(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));
}
