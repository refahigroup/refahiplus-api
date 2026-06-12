namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;

public sealed class SnappTripFlightOptions
{
    public string BaseUrl { get; set; } = "https://b2bapiv2.snapptrip.com/flight/";

    public string ApiBasePath { get; set; } = "api/v1";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;

    public int RetryCount { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 300;

    public int CircuitBreakerFailuresBeforeTrip { get; set; } = 5;

    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    public int BulkheadMaxParallelization { get; set; } = 50;

    public int BulkheadMaxQueuedActions { get; set; } = 100;
}
