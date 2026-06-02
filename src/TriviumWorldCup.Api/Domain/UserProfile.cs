namespace TriviumWorldCup.Api.Domain;

/// <summary>
/// Stores a user's chosen display name and country.
/// Document ID equals the AppUser.UserId (the auth identity key).
/// </summary>
public class UserProfile
{
    /// <summary>Equals AppUser.UserId — the stable, provider-issued identity key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>User-chosen display name; 2–30 characters, no leading/trailing whitespace.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 country code, e.g. "NL", "BE".</summary>
    public string CountryCode { get; set; } = string.Empty;
}
