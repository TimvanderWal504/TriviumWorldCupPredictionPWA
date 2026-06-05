using TriviumWorldCup.Api.Auth.Link;

namespace TriviumWorldCup.Api.Auth;

/// <summary>
/// Wires the auth abstraction into the DI container.
/// The active provider is selected by the "Auth:Provider" configuration key.
/// Supported values:
///   "link" — admin-managed users stored in DB, login via unique URL (default).
/// TWC-20 will add "entra" — no changes required outside this file.
/// </summary>
public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthAbstraction(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var providerName = configuration["Auth:Provider"] ?? "link";

        switch (providerName.ToLowerInvariant())
        {
            case "link":
                services.AddSingleton<IIdentityProvider, LinkIdentityProvider>();
                break;

            // TWC-20: case "entra": services.AddSingleton<IIdentityProvider, EntraIdentityProvider>(); break;

            default:
                throw new InvalidOperationException(
                    $"Unknown auth provider '{providerName}'. " +
                    "Set Auth:Provider to a supported value ('link').");
        }

        return services;
    }

    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentUserMiddleware>();
}
