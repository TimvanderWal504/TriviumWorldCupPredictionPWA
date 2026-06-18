using Marten;
using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tournament;

/// <summary>
/// Read-only knockout bracket endpoints.
/// No authentication required — bracket data is public.
/// </summary>
public static class KnockoutSlotEndpoints
{
    public static IEndpointRouteBuilder MapKnockoutSlotEndpoints(this IEndpointRouteBuilder routes)
    {
        // GET /knockout/slots — all 32 bracket slots with current status.
        routes.MapGet("/knockout/slots", async (IDocumentSession session, CancellationToken ct) =>
        {
            var slots = await session.Query<KnockoutSlot>()
                .OrderBy(s => s.Round)
                .ThenBy(s => s.SlotNumber)
                .ToListAsync(ct);

            var dtos = slots.Select(s => new KnockoutSlotDto(
                SlotKey:           s.SlotKey,
                Round:             s.Round.ToString(),
                SlotNumber:        s.SlotNumber,
                HomeTeamId:        s.HomeTeamId,
                AwayTeamId:        s.AwayTeamId,
                KickoffUtc:        s.KickoffUtc,
                Venue:             s.Venue,
                City:              s.City,
                Status:            s.Status.ToString(),
                HomeScore:         s.HomeScore,
                AwayScore:         s.AwayScore,
                PenaltyHomeScore:  s.PenaltyHomeScore,
                PenaltyAwayScore:  s.PenaltyAwayScore,
                WinnerTeamId:      s.WinnerTeamId
            ));

            return Results.Ok(dtos);
        })
        .WithName("GetKnockoutSlots")
        .WithTags("knockout")
        .WithSummary("Returns all 32 knockout bracket slots with current team assignments and results.")
        .CacheOutput("knockout-slots");

        return routes;
    }
}

/// <summary>Response DTO for a single knockout bracket slot.</summary>
public sealed record KnockoutSlotDto(
    string SlotKey,
    string Round,
    int SlotNumber,
    string? HomeTeamId,
    string? AwayTeamId,
    DateTimeOffset? KickoffUtc,
    string? Venue,
    string? City,
    string Status,
    int? HomeScore,
    int? AwayScore,
    int? PenaltyHomeScore,
    int? PenaltyAwayScore,
    string? WinnerTeamId);
