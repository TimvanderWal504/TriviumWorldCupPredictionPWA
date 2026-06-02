using TriviumWorldCup.Api.Domain;
using TriviumWorldCup.Api.Profiles;

namespace TriviumWorldCup.Api.Tests.Profiles;

/// <summary>
/// Unit tests for ProfileEndpoints.ValidateRequest — covers every validation
/// rule required by TWC-4 acceptance criteria.
/// No database required.
/// </summary>
public class ProfileValidationTests
{
    // ── Valid inputs ──────────────────────────────────────────────────────────

    [Fact]
    public void ValidRequest_ReturnsNull()
    {
        var request = new ProfileRequest("Alice", "NL");
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    [Fact]
    public void ValidRequest_MinimumLength_DisplayName_ReturnsNull()
    {
        // Exactly 2 characters — the minimum allowed
        var request = new ProfileRequest("AB", "GB");
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    [Fact]
    public void ValidRequest_MaximumLength_DisplayName_ReturnsNull()
    {
        // Exactly 30 characters — the maximum allowed
        var request = new ProfileRequest(new string('X', 30), "DE");
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("NL")]
    [InlineData("DE")]
    [InlineData("GB")]
    [InlineData("ZW")]   // last in the list alphabetically
    [InlineData("AF")]   // first in the list alphabetically
    public void ValidRequest_AllowsKnownCountryCodes(string code)
    {
        var request = new ProfileRequest("ValidName", code);
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    [Theory]
    [InlineData("us")]   // lower-case — should still be accepted (normalised by endpoint)
    [InlineData("nl")]
    public void ValidRequest_LowerCaseCountryCode_IsAccepted(string code)
    {
        // CountryCodes.IsValid is case-insensitive
        var request = new ProfileRequest("ValidName", code);
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    // ── Empty / null display name ─────────────────────────────────────────────

    [Fact]
    public void Validate_NullDisplayName_ReturnsError()
    {
        var request = new ProfileRequest(null, "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("DisplayName", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyDisplayName_ReturnsError()
    {
        var request = new ProfileRequest("", "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("DisplayName", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WhitespaceOnlyDisplayName_ReturnsError()
    {
        var request = new ProfileRequest("   ", "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("DisplayName", error, StringComparison.OrdinalIgnoreCase);
    }

    // ── Display name length ───────────────────────────────────────────────────

    [Fact]
    public void Validate_DisplayName_TooShort_OneChar_ReturnsError()
    {
        var request = new ProfileRequest("A", "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("2", error); // "at least 2 characters"
    }

    [Fact]
    public void Validate_DisplayName_TooLong_31Chars_ReturnsError()
    {
        var request = new ProfileRequest(new string('X', 31), "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("30", error); // "at most 30 characters"
    }

    [Fact]
    public void Validate_DisplayName_TooLong_100Chars_ReturnsError()
    {
        var request = new ProfileRequest(new string('A', 100), "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
    }

    // ── Leading / trailing whitespace in display name ─────────────────────────

    [Fact]
    public void Validate_DisplayName_LeadingWhitespace_TrimmedTo1Char_ReturnsError()
    {
        // "  A" trims to "A" which is 1 char — below minimum
        var request = new ProfileRequest("  A", "NL");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void Validate_DisplayName_TrailingWhitespace_TrimmedToValidLength_IsAccepted()
    {
        // "  Alice  " trims to "Alice" which is 5 chars — valid
        var request = new ProfileRequest("  Alice  ", "NL");
        Assert.Null(ProfileEndpoints.ValidateRequest(request));
    }

    // ── Country code validation ───────────────────────────────────────────────

    [Fact]
    public void Validate_NullCountryCode_ReturnsError()
    {
        var request = new ProfileRequest("Alice", null);
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("CountryCode", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_EmptyCountryCode_ReturnsError()
    {
        var request = new ProfileRequest("Alice", "");
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains("CountryCode", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("NETHER")]
    [InlineData("Netherlands")]
    [InlineData("123")]
    public void Validate_UnknownCountryCode_ReturnsBadRequest(string code)
    {
        var request = new ProfileRequest("Alice", code);
        var error = ProfileEndpoints.ValidateRequest(request);
        Assert.NotNull(error);
        Assert.Contains(code, error); // error message names the bad code
    }
}
