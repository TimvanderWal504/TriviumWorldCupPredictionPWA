namespace TriviumWorldCup.Api.Auth;

/// <summary>
/// Resolves the current user via <see cref="IIdentityProvider"/> and stores
/// it in <see cref="HttpContext.Items"/> so downstream handlers can retrieve
/// it with <see cref="HttpContextExtensions.GetAppUser"/>.
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    internal const string HttpContextKey = "AppUser";

    public async Task InvokeAsync(HttpContext context, IIdentityProvider provider)
    {
        var user = await provider.GetCurrentUserAsync(context, context.RequestAborted);
        context.Items[HttpContextKey] = user;
        await next(context);
    }
}

public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the resolved <see cref="AppUser"/> for the current request.
    /// Returns <see cref="AppUser.Anonymous"/> if the middleware has not run
    /// or the user is unauthenticated.
    /// </summary>
    public static AppUser GetAppUser(this HttpContext context) =>
        context.Items[CurrentUserMiddleware.HttpContextKey] as AppUser
        ?? AppUser.Anonymous;
}
