using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;

namespace Refahi.Modules.Flights.Infrastructure.Providers;

public sealed class FlightProviderFactory : IFlightProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly FlightProviderType _defaultProvider;

    public FlightProviderFactory(
        IServiceProvider serviceProvider,
        FlightProviderType defaultProvider = FlightProviderType.SnappTrip)
    {
        _serviceProvider = serviceProvider;
        _defaultProvider = defaultProvider;
    }

    public IFlightProvider GetProvider(FlightProviderType providerType)
    {
        return providerType switch
        {
            FlightProviderType.SnappTrip => _serviceProvider.GetRequiredService<SnappTripFlightProvider>(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, "Unknown flight provider type.")
        };
    }

    public IFlightProvider GetDefaultProvider()
    {
        return GetProvider(_defaultProvider);
    }
}
