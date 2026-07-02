using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using TriviumWorldCup.Api.Predictions;

namespace TriviumWorldCup.Api.Tests.Predictions;

/// <summary>
/// TWC-52: "POST /predictions/group/inject" was a self-service bulk-upsert of group
/// predictions whose only guard was user.IsAuthenticated — it bypassed IsLocked(fixture)
/// entirely, letting any invited user write predictions for completed fixtures and gain
/// points on the next scoring recompute.
///
/// The endpoint has been deleted. The admin-gated equivalent
/// (POST /admin/users/{userId}/predictions/inject, gated with IsInRole("admin")) supersedes
/// it. Every remaining write path on this route group (POST/PUT /predictions/group/{fixtureId})
/// enforces IsLocked(fixture) server-side (see GroupPredictionLockTests for the IsLocked
/// predicate itself).
///
/// These tests prove — at the route-table level, no database required — that the
/// lock-bypassing bulk route no longer exists for non-admin callers.
/// </summary>
public class GroupPredictionInjectRemovedTests
{
    private static IReadOnlyList<RouteEndpoint> MapAndGetEndpoints()
    {
        var builder = WebApplication.CreateSlimBuilder();

        // RequestDelegateFactory needs to resolve every non-primitive handler parameter type as
        // a DI service in order to build endpoint metadata. IDocumentSession is never actually
        // invoked here — we only inspect the route table — so a never-called factory is enough.
        builder.Services.AddSingleton<IDocumentSession>(_ =>
            throw new InvalidOperationException("Not resolved — route-table inspection only."));

        var app = builder.Build();
        app.MapGroupPredictionEndpoints();

        // IEndpointRouteBuilder.DataSources is populated synchronously by each MapGet/MapPost/
        // MapPut call — no need to build/start the host to inspect the route table.
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    [Fact]
    public void NoRoute_ContainsInject_OnGroupPredictionEndpoints()
    {
        var endpoints = MapAndGetEndpoints();

        var routePatterns = endpoints.Select(e => e.RoutePattern.RawText).ToList();

        Assert.DoesNotContain(routePatterns, p => p is not null && p.Contains("inject", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GroupPredictionEndpoints_OnlyRegistersRootGetAndFixtureIdPostPut()
    {
        var endpoints = MapAndGetEndpoints();

        // Expect exactly: GET /predictions/group/, POST /predictions/group/{fixtureId},
        // PUT /predictions/group/{fixtureId}. No bulk/inject route.
        var routePatterns = endpoints.Select(e => e.RoutePattern.RawText).ToList();

        Assert.Equal(3, routePatterns.Count);
        Assert.All(routePatterns, p => Assert.DoesNotContain("inject", p, StringComparison.OrdinalIgnoreCase));
    }
}
