using System.Text.Json;
using TriviumWorldCup.Api.Leaderboard;

namespace TriviumWorldCup.Api.Tests.Leaderboard;

/// <summary>
/// TWC-54: GET /leaderboard is public (no auth) and must never expose member email
/// addresses. Verifies the public leaderboard DTO carries no email field, at both
/// the type level and the serialized-JSON level (belt and braces — catches any
/// future re-introduction of an Email property or a [JsonPropertyName] alias).
/// </summary>
public class LeaderboardEntryDtoTests
{
    [Fact]
    public void LeaderboardEntryDto_HasNoEmailProperty()
    {
        var properties = typeof(LeaderboardEntryDto).GetProperties();

        Assert.DoesNotContain(properties, p => string.Equals(p.Name, "Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LeaderboardEntryDto_SerializedJson_HasNoEmailProperty()
    {
        var dto = new LeaderboardEntryDto(
            Rank:             1,
            UserId:           "user-1",
            DisplayName:      "Alice",
            CountryCode:      "NL",
            TotalPoints:      42,
            GroupMatchPoints: 30,
            ChampionPoints:   10,
            GoldenSixPoints:  2);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        using var doc = JsonDocument.Parse(json);
        var propertyNames = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.DoesNotContain(propertyNames, name => string.Equals(name, "email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeaderboardEntryDto_SerializedJson_StillContainsExpectedPublicFields()
    {
        var dto = new LeaderboardEntryDto(
            Rank:             1,
            UserId:           "user-1",
            DisplayName:      "Alice",
            CountryCode:      "NL",
            TotalPoints:      42,
            GroupMatchPoints: 30,
            ChampionPoints:   10,
            GoldenSixPoints:  2);

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        using var doc = JsonDocument.Parse(json);
        var propertyNames = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        Assert.Contains("rank", propertyNames);
        Assert.Contains("userId", propertyNames);
        Assert.Contains("displayName", propertyNames);
        Assert.Contains("countryCode", propertyNames);
        Assert.Contains("totalPoints", propertyNames);
        Assert.Contains("groupMatchPoints", propertyNames);
        Assert.Contains("championPoints", propertyNames);
        Assert.Contains("goldenSixPoints", propertyNames);
    }
}
