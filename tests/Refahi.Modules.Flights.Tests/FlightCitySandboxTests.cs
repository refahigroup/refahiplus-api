using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Flights.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;
using Xunit;

namespace Refahi.Modules.Flights.Tests;

public sealed class FlightSandboxFactAttribute : FactAttribute
{
    public FlightSandboxFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLIGHT_SANDBOX_BASE_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLIGHT_SANDBOX_API_KEY")))
            Skip = "Requires an explicitly configured SnappTrip sandbox URL and API key; production settings are never used.";
    }
}

public sealed class FlightCitySandboxTests
{
    [FlightSandboxFact]
    public async Task Sandbox_AcceptsInternationalCitySearch()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var options = Options.Create(new SnappTripFlightOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("FLIGHT_SANDBOX_BASE_URL")!,
            ApiKey = Environment.GetEnvironmentVariable("FLIGHT_SANDBOX_API_KEY")!
        });
        var api = new SnappTripFlightApiClient(http, NullLogger<SnappTripFlightApiClient>.Instance, options);
        var provider = new SnappTripFlightProvider(api, NullLogger<SnappTripFlightProvider>.Instance);
        var response = await provider.SearchAsync(new FlightSearchRequest(1, 0, 0, false,
            [new FlightSearchLeg(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), "IKA", "IST", "Airport", "City")],
            new FlightTravelPreference("Economy", "OneWay")));
        // No booking, order, issuance or payment is performed. An empty successful search is valid.
        Assert.True(response.Success, "The sandbox did not accept the CITY search. Inspect masked provider diagnostics.");
    }
}
