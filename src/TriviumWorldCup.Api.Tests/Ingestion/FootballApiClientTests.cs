using System.Net;
using TriviumWorldCup.Api.Ingestion;

namespace TriviumWorldCup.Api.Tests.Ingestion;

/// <summary>
/// Unit tests for <see cref="FootballApiClient"/> using a fake <see cref="HttpMessageHandler"/>
/// so no real HTTP calls are made. No database required.
///
/// TWC-66 acceptance criteria covered:
///   - 429 quota detection is consistent across all API-Football call paths (events AND
///     fixture fetches), not just the events endpoint. Both must raise the same recognisable
///     exception shape: HttpRequestException whose InnerException is an
///     InvalidOperationException with message "Quota exceeded" — this is what
///     ResultIngestionJob's catch clauses (and RecheckPostponedFixturesAsync) pattern-match on.
/// </summary>
public class FootballApiClientTests
{
    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content = "") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content),
            };
            return Task.FromResult(response);
        }
    }

    private static FootballApiClient CreateClient(HttpStatusCode statusCode, string content = "")
    {
        var handler = new FakeHttpMessageHandler(statusCode, content);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://v3.football.api-sports.io/") };
        return new FootballApiClient(httpClient);
    }

    private static bool IsQuotaExceededShape(Exception ex) =>
        ex is HttpRequestException { InnerException: InvalidOperationException { Message: "Quota exceeded" } };

    // ── GetAllEventsAsync — existing behavior, unchanged by the refactor ─────────

    [Fact]
    public async Task GetAllEventsAsync_429_ThrowsQuotaExceededShape()
    {
        var client = CreateClient(HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllEventsAsync(12345));

        Assert.True(IsQuotaExceededShape(ex));
    }

    // ── GetFixturesByDateAsync / GetAllFixturesForSeasonAsync — the actual bug ───

    [Fact]
    public async Task GetFixturesByDateAsync_429_ThrowsSameQuotaExceededShapeAsEvents()
    {
        // Before the fix, FetchFixturesAsync only called EnsureSuccessStatusCode(), which
        // throws a generic HttpRequestException with no recognisable inner exception —
        // RecheckPostponedFixturesAsync's catch clause (which pattern-matches on the
        // InvalidOperationException("Quota exceeded") shape) would not catch it, so quota
        // exhaustion on a fixture fetch was misreported as a generic failure.
        var client = CreateClient(HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetFixturesByDateAsync(DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.True(IsQuotaExceededShape(ex));
    }

    [Fact]
    public async Task GetAllFixturesForSeasonAsync_429_ThrowsSameQuotaExceededShapeAsEvents()
    {
        var client = CreateClient(HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllFixturesForSeasonAsync());

        Assert.True(IsQuotaExceededShape(ex));
    }

    [Fact]
    public async Task GetFixturesByDateAsync_429_ExceptionMessageMentionsQuota()
    {
        var client = CreateClient(HttpStatusCode.TooManyRequests);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetFixturesByDateAsync(DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.Contains("quota", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Non-429 failures still surface as ordinary HTTP errors ───────────────────

    [Fact]
    public async Task GetFixturesByDateAsync_500_ThrowsWithoutQuotaShape()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetFixturesByDateAsync(DateOnly.FromDateTime(DateTime.UtcNow)));

        Assert.False(IsQuotaExceededShape(ex));
    }

    [Fact]
    public async Task GetAllEventsAsync_500_ThrowsWithoutQuotaShape()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAllEventsAsync(12345));

        Assert.False(IsQuotaExceededShape(ex));
    }

    // ── Happy path still works after the refactor ────────────────────────────────

    [Fact]
    public async Task GetFixturesByDateAsync_200_EmptyResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"response":[]}""");

        var result = await client.GetFixturesByDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllEventsAsync_200_EmptyResponse_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.OK, """{"response":[]}""");

        var result = await client.GetAllEventsAsync(12345);

        Assert.Empty(result);
    }
}
