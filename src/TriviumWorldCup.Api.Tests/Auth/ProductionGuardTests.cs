using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TriviumWorldCup.Api.Auth;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Verifies that the mock provider is refused in Production and accepted in non-Production environments.
/// </summary>
public class ProductionGuardTests
{
    [Fact]
    public void AddAuthAbstraction_MockProviderInProduction_Throws()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("mock");
        var env = new TestHostEnvironment("Production");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAuthAbstraction(config, env));

        Assert.Contains("mock identity provider", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Production", ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Demo")]
    public void AddAuthAbstraction_MockProviderInNonProduction_DoesNotThrow(string envName)
    {
        var services = new ServiceCollection();
        var config = BuildConfig("mock");
        var env = new TestHostEnvironment(envName);

        // Should not throw
        services.AddAuthAbstraction(config, env);

        var sp = services.BuildServiceProvider();
        var provider = sp.GetRequiredService<IIdentityProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddAuthAbstraction_UnknownProvider_Throws()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("bogus-provider");
        var env = new TestHostEnvironment("Development");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAuthAbstraction(config, env));

        Assert.Contains("bogus-provider", ex.Message);
    }

    [Fact]
    public void AddAuthAbstraction_DefaultsToMockWhenKeyMissing_InDevelopment()
    {
        var services = new ServiceCollection();
        // No "Auth:Provider" key in config — should default to mock
        var config = BuildConfig(providerValue: null);
        var env = new TestHostEnvironment("Development");

        services.AddAuthAbstraction(config, env);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<IIdentityProvider>());
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig(string? providerValue)
    {
        var dict = new Dictionary<string, string?>();
        if (providerValue is not null)
            dict["Auth:Provider"] = providerValue;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    /// <summary>Minimal IHostEnvironment stub for testing.</summary>
    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
