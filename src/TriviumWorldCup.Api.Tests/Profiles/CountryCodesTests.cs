using TriviumWorldCup.Api.Domain;

namespace TriviumWorldCup.Api.Tests.Profiles;

/// <summary>
/// Tests for the CountryCodes static helper.
/// </summary>
public class CountryCodesTests
{
    [Fact]
    public void All_Contains_AtLeast_200_Codes()
    {
        // ISO 3166-1 has 249 entries; we check a conservative lower bound.
        Assert.True(CountryCodes.All.Count >= 200,
            $"Expected at least 200 country codes, got {CountryCodes.All.Count}");
    }

    [Theory]
    [InlineData("NL")]
    [InlineData("DE")]
    [InlineData("US")]
    [InlineData("GB")]
    [InlineData("FR")]
    [InlineData("BR")]
    [InlineData("JP")]
    [InlineData("ZA")]
    [InlineData("AF")]
    [InlineData("ZW")]
    public void IsValid_ReturnsTrueForKnownCodes(string code)
    {
        Assert.True(CountryCodes.IsValid(code));
    }

    [Theory]
    [InlineData("nl")]   // lower-case
    [InlineData("Nl")]   // mixed-case
    [InlineData("de")]
    public void IsValid_IsCaseInsensitive(string code)
    {
        Assert.True(CountryCodes.IsValid(code));
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("NETHER")]
    [InlineData("123")]
    [InlineData("")]
    public void IsValid_ReturnsFalseForUnknownCodes(string code)
    {
        Assert.False(CountryCodes.IsValid(code));
    }

    [Fact]
    public void IsValid_ReturnsFalseForNull()
    {
        Assert.False(CountryCodes.IsValid(null));
    }

    [Fact]
    public void All_CodesAreUniqueUpperCase()
    {
        // Codes stored in the set are upper-case and distinct (HashSet guarantees uniqueness).
        foreach (var code in CountryCodes.All)
            Assert.Equal(code, code.ToUpperInvariant());
    }
}
