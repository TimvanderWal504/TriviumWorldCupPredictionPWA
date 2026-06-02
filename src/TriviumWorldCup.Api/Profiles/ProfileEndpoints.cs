using Marten;
using Microsoft.AspNetCore.Mvc;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Profiles;

/// <summary>
/// Profile API endpoints: GET / POST / PUT /profile.
/// All three require an authenticated user; ownership is enforced server-side.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/profile").WithTags("profile");

        // GET /profile — returns the current user's profile, or 404.
        group.MapGet("/", async (HttpContext context, IDocumentSession session, CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var profile = await session.LoadAsync<UserProfile>(user.UserId, ct);
            return profile is null
                ? Results.NotFound()
                : Results.Ok(ToDto(profile));
        })
        .WithName("GetProfile")
        .WithSummary("Returns the current user's profile.");

        // POST /profile — creates a new profile; 409 if one already exists.
        group.MapPost("/", async (
            HttpContext context,
            [FromBody] ProfileRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var validation = ValidateRequest(request);
            if (validation is not null)
                return Results.BadRequest(new { error = validation });

            var existing = await session.LoadAsync<UserProfile>(user.UserId, ct);
            if (existing is not null)
                return Results.Conflict(new { error = "Profile already exists. Use PUT /profile to update." });

            var profile = new UserProfile
            {
                Id          = user.UserId,
                DisplayName = request.DisplayName!.Trim(),
                CountryCode = request.CountryCode!.ToUpperInvariant(),
            };

            session.Store(profile);
            await session.SaveChangesAsync(ct);

            return Results.Created("/profile", ToDto(profile));
        })
        .WithName("CreateProfile")
        .WithSummary("Creates the current user's profile.");

        // PUT /profile — updates display name and/or country.
        group.MapPut("/", async (
            HttpContext context,
            [FromBody] ProfileRequest request,
            IDocumentSession session,
            CancellationToken ct) =>
        {
            var user = context.GetAppUser();
            if (!user.IsAuthenticated)
                return Results.Unauthorized();

            var validation = ValidateRequest(request);
            if (validation is not null)
                return Results.BadRequest(new { error = validation });

            var profile = await session.LoadAsync<UserProfile>(user.UserId, ct);
            if (profile is null)
                return Results.NotFound(new { error = "Profile not found. Use POST /profile to create one." });

            profile.DisplayName = request.DisplayName!.Trim();
            profile.CountryCode = request.CountryCode!.ToUpperInvariant();

            session.Store(profile);
            await session.SaveChangesAsync(ct);

            return Results.Ok(ToDto(profile));
        })
        .WithName("UpdateProfile")
        .WithSummary("Updates the current user's profile.");

        return routes;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    /// <summary>Returns an error message if the request is invalid, otherwise null.</summary>
    public static string? ValidateRequest(ProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return "DisplayName must not be empty.";

        var trimmed = request.DisplayName.Trim();

        if (trimmed.Length < 2)
            return "DisplayName must be at least 2 characters.";

        if (trimmed.Length > 30)
            return "DisplayName must be at most 30 characters.";

        if (string.IsNullOrWhiteSpace(request.CountryCode))
            return "CountryCode must not be empty.";

        if (!CountryCodes.IsValid(request.CountryCode))
            return $"CountryCode '{request.CountryCode}' is not a recognised ISO 3166-1 alpha-2 code.";

        return null;
    }

    private static ProfileDto ToDto(UserProfile p) =>
        new(p.Id, p.DisplayName, p.CountryCode);
}

/// <summary>Request body for POST and PUT /profile.</summary>
public sealed record ProfileRequest(string? DisplayName, string? CountryCode);

/// <summary>Response body returned from all profile endpoints.</summary>
public sealed record ProfileDto(string UserId, string DisplayName, string CountryCode);
