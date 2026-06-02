using TriviumWorldCup.Api.Auth.Mock;

namespace TriviumWorldCup.Api.Auth;

/// <summary>
/// Wires the auth abstraction into the DI container.
/// The active provider is selected by the "Auth:Provider" configuration key.
/// Supported values: "mock" (dev/demo only).
/// TWC-20 will add "entra" — no changes required outside this file.
/// </summary>
public static class AuthServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IIdentityProvider"/> and the cookie data-protection
    /// services. Throws <see cref="InvalidOperationException"/> if the mock
    /// provider is selected in a Production environment.
    /// </summary>
    public static IServiceCollection AddAuthAbstraction(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var providerName = configuration["Auth:Provider"]
            ?? "mock"; // default to mock for local/dev convenience

        switch (providerName.ToLowerInvariant())
        {
            case "mock":
                if (environment.IsProduction())
                    throw new InvalidOperationException(
                        "The mock identity provider cannot be used in Production. " +
                        "Set Auth:Provider to a real provider (e.g. 'entra') " +
                        "or change ASPNETCORE_ENVIRONMENT.");

                services.AddSingleton<IIdentityProvider, MockIdentityProvider>();
                break;

            // TWC-20: case "entra": services.AddSingleton<IIdentityProvider, EntraIdentityProvider>(); break;

            default:
                throw new InvalidOperationException(
                    $"Unknown auth provider '{providerName}'. " +
                    "Set Auth:Provider to a supported value (e.g. 'mock').");
        }

        return services;
    }

    /// <summary>
    /// Adds <see cref="CurrentUserMiddleware"/> to the pipeline.
    /// Must be called after routing.
    /// </summary>
    public static IApplicationBuilder UseCurrentUser(this IApplicationBuilder app)
        => app.UseMiddleware<CurrentUserMiddleware>();
}
