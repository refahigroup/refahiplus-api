using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Services;
using Refahi.Modules.Commerce.Infrastructure;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class CommerceProviderRegistrationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Factory_resolves_each_enabled_provider(bool aabsarEnabled, bool touristPanelEnabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStringName"] = "CommerceTests",
            ["ConnectionStrings:CommerceTests"] = "Host=localhost;Database=commerce_tests;Username=test;Password=test",
            ["Commerce:Providers:Aabsar:Enabled"] = aabsarEnabled.ToString(),
            ["Commerce:Providers:Aabsar:AccessToken"] = "test-token",
            ["Commerce:Providers:TouristPanel:Enabled"] = touristPanelEnabled.ToString(),
            ["Commerce:Providers:TouristPanel:Tenant"] = "00000000-0000-0000-0000-000000000001",
            ["Commerce:Providers:TouristPanel:ClientId"] = "test-client",
            ["Commerce:Providers:TouristPanel:ClientSecret"] = "test-secret",
            ["Commerce:Providers:TouristPanel:Username"] = "test-user",
            ["Commerce:Providers:TouristPanel:Password"] = "test-password"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICommercePricingService, CommercePricingService>();
        services.RegisterInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ICommerceProviderFactory>();

        var expectedKeys = new List<string>();
        if (aabsarEnabled) expectedKeys.Add("aabsar");
        if (touristPanelEnabled) expectedKeys.Add("touristpanel");
        Assert.Equal(expectedKeys, factory.GetEnabledProviders().Select(x => x.Key).OrderBy(x => x));
        foreach (var instance in factory.GetEnabledProviders())
            Assert.Same(instance, factory.GetRequired(instance.Key.ToUpperInvariant()));
    }
}
