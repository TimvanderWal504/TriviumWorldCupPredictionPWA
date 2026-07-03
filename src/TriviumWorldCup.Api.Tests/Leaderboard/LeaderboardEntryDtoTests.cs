using System.Text.Json;
using TriviumWorldCup.Api.Leaderboard;

namespace TriviumWorldCup.Api.Tests.Leaderboard;

/// <summary>
/// TWC-54: GET /leaderboard is public (no auth) and must never expose a full member
/// email address. The DTO carries no Email property/alias — only MemberHandle, the
/// masked email local-part (before '@'), computed server-side.
/// </summary>
public class LeaderboardEntryDtoTests
{
    [Fact]
    public void LeaderboardEntryDto_SerializedJson_StillContainsExpectedPublicFields()
    {
        var dto = new LeaderboardEntryDto(
            Rank:             1,
            UserId:           "user-1",
            DisplayName:      "Alice",
            CountryCode:      "NL",
            MemberHandle:     "alice",
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
        Assert.Contains("memberHandle", propertyNames);
        Assert.Contains("totalPoints", propertyNames);
        Assert.Contains("groupMatchPoints", propertyNames);
        Assert.Contains("championPoints", propertyNames);
        Assert.Contains("goldenSixPoints", propertyNames);
    }
}
