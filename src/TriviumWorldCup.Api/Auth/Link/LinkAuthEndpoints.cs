using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Auth.Link;

/// <summary>
/// Auth endpoints for the link-based identity provider.
/// GET /auth/link/login?id=  — validates the login key, issues a session cookie, redirects to /.
/// POST /auth/link/logout    — clears the session cookie.
/// GET /auth/me              — provider-agnostic user bootstrap (reports authProvider: "link").
/// </summary>
public static class LinkAuthEndpoints
{
    public static IEndpointRouteBuilder MapLinkAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /auth/link/login?id=<userId>
        // On success: sets HttpOnly session cookie, redirects to /.
        // On failure: redirects to / without cookie — React app shows the login screen.
        routes.MapGet("/auth/link/login", async (
            HttpContext context,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var id = context.Request.Query["id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(id))
            {
                await using var session = store.LightweightSession();
                var user = await session.LoadAsync<InviteUser>(id, ct);
                if (user is not null)
                {
                    context.Response.Cookies.Append(
                        LinkIdentityProvider.CookieName,
                        user.Id,
                        new CookieOptions
                        {
                            HttpOnly  = true,
                            SameSite  = SameSiteMode.Lax,
                            Secure    = context.Request.IsHttps,
                            MaxAge    = TimeSpan.FromDays(30),
                        });
                }
            }

            return Results.Redirect("/");
        })
        .WithName("LinkLogin")
        .WithTags("auth-link")
        .WithSummary("Validates a login-link ID and issues a 30-day session cookie.");

        // POST /auth/link/logout
        routes.MapPost("/auth/link/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete(LinkIdentityProvider.CookieName);
            return Results.Ok(new { status = "logged-out" });
        })
        .WithName("LinkLogout")
        .WithTags("auth-link")
        .WithSummary("Signs out by clearing the session cookie.");

        // GET /auth/me — provider-agnostic; always registered alongside this provider
        routes.MapGet("/auth/me", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Ok(new { authenticated = false, user = (object?)null, authProvider = "link" });

            var profile = await session.LoadAsync<UserProfile>(user.UserId, ct);
            var displayName = profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? profile.DisplayName
                : user.DisplayName;

            return Results.Ok(new
            {
                authenticated = true,
                user          = new { user.UserId, displayName, user.Roles },
                authProvider  = "link",
            });
        })
        .WithName("AuthMe")
        .WithTags("auth")
        .WithSummary("Returns the current authenticated user.");

        return routes;
    }
}
