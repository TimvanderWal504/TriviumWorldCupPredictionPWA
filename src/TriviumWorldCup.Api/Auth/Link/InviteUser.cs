namespace TriviumWorldCup.Api.Auth.Link;

/// <summary>
/// An admin-managed user for the link-based auth provider.
/// The Id is a stable Guid string that is embedded in the login URL — it is
/// the user's only credential, so treat it like a password.
/// </summary>
public class InviteUser
{
    /// <summary>Marten document Id — also the value embedded in the login URL.</summary>
    public string Id { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string[] Roles { get; set; } = ["user"];

    public DateTimeOffset CreatedAt { get; set; }
}
