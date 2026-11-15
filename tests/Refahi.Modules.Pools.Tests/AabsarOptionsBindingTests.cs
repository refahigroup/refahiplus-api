using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar;
using Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Options;
using Xunit;

namespace Refahi.Modules.Pools.Tests;

public sealed class AabsarOptionsBindingTests
{
    [Fact]
    public void AddAabsarProvider_binds_hierarchical_configuration_section()
    {
        const string accessToken = "test-access-token";
        const string baseUrl = "https://provider.test/api/";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Pools:Providers:Aabsar:AccessToken"] = accessToken,
                    ["Pools:Providers:Aabsar:BaseUrl"] = baseUrl,
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.AddAabsarProvider(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AabsarOptions>>().Value;

        Assert.Equal(accessToken, options.AccessToken);
        Assert.Equal(baseUrl, options.BaseUrl);
    }

    [Fact]
    public void Disabled_provider_does_not_require_a_credential()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pools:Providers:Aabsar:Enabled"] = "false",
            ["Pools:Providers:Aabsar:AccessToken"] = ""
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks();
        services.AddAabsarProvider(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<AabsarOptions>>().Value.Enabled);
        Assert.DoesNotContain(provider.GetRequiredService<IOptions<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckServiceOptions>>().Value.Registrations,
            registration => registration.Name == "pools-aabsar");
    }

    [Fact]
    public void Enabled_provider_fails_validation_without_a_credential()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pools:Providers:Aabsar:Enabled"] = "true",
            ["Pools:Providers:Aabsar:AccessToken"] = ""
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks();
        services.AddAabsarProvider(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<AabsarOptions>>().Value);
    }
}
