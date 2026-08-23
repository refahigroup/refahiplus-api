using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Options;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

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
                    ["Commerce:Providers:Aabsar:AccessToken"] = accessToken,
                    ["Commerce:Providers:Aabsar:BaseUrl"] = baseUrl,
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
}
