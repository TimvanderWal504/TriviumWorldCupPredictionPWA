using Microsoft.AspNetCore.Mvc;

namespace TriviumWorldCup.Api.Auth.Mock;

/// <summary>
/// Lightweight endpoint group for the mock auth provider.
/// Exposes user switching without real credentials — dev/demo only.
/// These endpoints are only registered when the mock provider is active.
/// </summary>
public static class MockAuthEndpoints
{
    public static IEndpointRouteBuilder MapMockAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth/mock")
            .WithTags("auth-mock");

        // GET /auth/mock/users — list seeded demo users
        group.MapGet("/users", () =>
            Results.Ok(MockUsers.All.Select(u => new
            {
                u.UserId,
                u.DisplayName,
                u.Roles,
            })))
            .WithName("MockGetUsers")
            .WithSummary("List seeded demo users (mock provider only).");

        // POST /auth/mock/login — sign in as a demo user
        group.MapPost("/login", (
            [FromBody] MockLoginRequest request,
            HttpContext context) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserId))
                return Results.BadRequest(new { error = "UserId is required." });

            var user = MockUsers.FindById(request.UserId);
            if (user is null)
                return Results.NotFound(new { error = $"Unknown userId: {request.UserId}" });

            context.Response.Cookies.Append(
                MockIdentityProvider.CookieName,
                user.UserId,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    // Secure = true in production but mock is never production
                    Secure = false,
                    MaxAge = TimeSpan.FromDays(1),
                });

            return Results.Ok(new { user.UserId, user.DisplayName, user.Roles });
        })
        .WithName("MockLogin")
        .WithSummary("Sign in as a demo user (mock provider only).");

        // POST /auth/mock/logout — clear the session cookie
        group.MapPost("/logout", (HttpContext context) =>
        {
            context.Response.Cookies.Delete(MockIdentityProvider.CookieName);
            return Results.Ok(new { status = "logged-out" });
        })
        .WithName("MockLogout")
        .WithSummary("Sign out (mock provider only).");

        // GET /auth/me — returns the current user (provider-agnostic, useful for client bootstrap)
        routes.MapGet("/auth/me", (HttpContext context) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Ok(new { authenticated = false, user = (object?)null });

            return Results.Ok(new
            {
                authenticated = true,
                user = new { user.UserId, user.DisplayName, user.Roles }
            });
        })
        .WithName("AuthMe")
        .WithTags("auth")
        .WithSummary("Returns the current authenticated user.");

        return routes;
    }
}

/// <summary>Request body for POST /auth/mock/login.</summary>
public sealed record MockLoginRequest(string UserId);
