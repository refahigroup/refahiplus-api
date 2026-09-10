using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class SnappTripFlightOptionsTests
{
    [Theory]
    [InlineData("5", 5)]
    [InlineData("0", 0)]
    public void Configuration_AcceptsValidCharterCommissionPercent(
        string configuredValue,
        decimal expected
    )
    {
        using var provider = BuildProvider(configuredValue);

        var options = provider.GetRequiredService<IOptions<SnappTripFlightOptions>>().Value;

        Assert.Equal(expected, options.CharterCommissionPercent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    [InlineData("5.123")]
    public void Configuration_RejectsMissingOrInvalidCharterCommissionPercent(
        string? configuredValue
    )
    {
        using var provider = BuildProvider(configuredValue);

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<SnappTripFlightOptions>>().Value
        );
    }

    private static ServiceProvider BuildProvider(string? charterCommissionPercent)
    {
        var values = new Dictionary<string, string?>
        {
            ["Flights:Providers:SnappTrip:BaseUrl"] = "https://example.test/flight/",
            ["Flights:Providers:SnappTrip:ApiKey"] = "test-key",
        };

        if (charterCommissionPercent is not null)
            values["Flights:Providers:SnappTrip:CharterCommissionPercent"] =
                charterCommissionPercent;

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.UseSnappTripFlightProvider(configuration);

        return services.BuildServiceProvider();
    }
}
