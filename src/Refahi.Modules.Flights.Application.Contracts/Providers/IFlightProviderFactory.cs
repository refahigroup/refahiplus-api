namespace Refahi.Modules.Flights.Application.Contracts.Providers;

public interface IFlightProviderFactory
{
    IFlightProvider GetProvider(FlightProviderType providerType);

    IFlightProvider GetDefaultProvider();
}
