using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TriviumWorldCup.Api.Auth;
using TriviumWorldCup.Api.Auth.Link;

namespace TriviumWorldCup.Api.Tests.Auth;

/// <summary>
/// Verifies AddAuthAbstraction registers the correct provider and rejects unknown values.
/// </summary>
public class ProductionGuardTests
{
    [Fact]
    public void AddAuthAbstraction_LinkProvider_RegistersLinkIdentityProvider()
    {
        var services = new ServiceCollection();
        var config = BuildConfig("link");
        var env = new TestHostEnvironment("Production");

        services.AddAuthAbstraction(config, env);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IIdentityProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(LinkIdentityProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void AddAuthAbstraction_DefaultsToLink_WhenKeyMissing()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(providerValue: null); // no "Auth:Provider" key
        var env = new TestHostEnvironment("Production");

        services.AddAuthAbstraction(config, env);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IIdentityProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(LinkIdentityProvider), descriptor.ImplementationType);
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
