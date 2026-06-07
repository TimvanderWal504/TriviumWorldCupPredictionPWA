using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Auth.Link;

file record SignupRequest(string? Email);
file record LoginRequest(string? Email, string? Token);

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
        // POST /auth/link/signup
        // Body: { email }
        // Creates an InviteUser for allowed email domains; returns { token } once.
        routes.MapPost("/auth/link/signup", async (
            SignupRequest request,
            IConfiguration config,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return Results.BadRequest(new { error = "Vul een geldig e-mailadres in." });

            var domain = email.Split('@')[1];
            var allowedDomains = config.GetSection("Auth:Link:AllowedDomains").Get<string[]>() ?? [];
            if (allowedDomains.Length == 0 || !allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase))
                return Results.UnprocessableEntity(new { error = "Dit e-maildomein is niet toegestaan." });

            await using var session = store.LightweightSession();
            var existing = await session.Query<InviteUser>()
                .Where(u => u.Email == email)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return Results.Conflict(new { error = "Er bestaat al een account voor dit e-mailadres." });

            var localPart = email.Split('@')[0];
            var displayName = char.ToUpperInvariant(localPart[0]) + localPart[1..];
            var newUser = new InviteUser
            {
                Id          = Guid.NewGuid().ToString(),
                Email       = email,
                DisplayName = displayName,
                CreatedAt   = DateTimeOffset.UtcNow,
            };
            session.Store(newUser);
            await session.SaveChangesAsync(ct);

            return Results.Ok(new { token = newUser.Id });
        })
        .WithName("LinkSignup")
        .WithTags("auth-link")
        .WithSummary("Self-service signup — maakt een InviteUser aan voor toegestane e-maildomeinen en retourneert eenmalig de token.")
        .AllowAnonymous();

        // POST /auth/link/login
        // Body: { email, token }
        // Looks up the user by email, verifies the token (= GUID Id), issues session cookie.
        routes.MapPost("/auth/link/login", async (
            LoginRequest request,
            HttpContext context,
            IDocumentStore store,
            CancellationToken ct) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant();
            var token = request.Token?.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "E-mailadres en token zijn verplicht." });

            await using var session = store.LightweightSession();
            var user = await session.Query<InviteUser>()
                .Where(u => u.Email == email)
                .FirstOrDefaultAsync(ct);

            if (user is null || user.Id != token)
                return Results.Unauthorized();

            context.Response.Cookies.Append(
                LinkIdentityProvider.CookieName,
                user.Id,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure   = context.Request.IsHttps,
                    MaxAge   = TimeSpan.FromDays(30),
                });

            return Results.Ok(new { authenticated = true });
        })
        .WithName("LinkFormLogin")
        .WithTags("auth-link")
        .WithSummary("Form-login — valideert e-mail + token en geeft een 30-daagse sessiecookie.")
        .AllowAnonymous();

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
