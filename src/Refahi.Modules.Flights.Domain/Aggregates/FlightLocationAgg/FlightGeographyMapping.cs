namespace Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;

// Reviewed geography is imported explicitly, never learned during discovery requests.
public sealed class AirportGeographyMapping
{
    private AirportGeographyMapping() { }
    public string AirportIataCode { get; private set; } = "";
    public int CityId { get; private set; }
    public string SourceIdentity { get; private set; } = "";
    public string MatchMethod { get; private set; } = "";
    public string Evidence { get; private set; } = "{}";
    public string SourceVersion { get; private set; } = "";
    public DateOnly SourceSnapshotDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class SnappTripFlightCityMapping
{
    private SnappTripFlightCityMapping() { }
    public string ProviderCityCode { get; private set; } = "";
    public int CityId { get; private set; }
    public string SourceIdentity { get; private set; } = "";
    public string MatchMethod { get; private set; } = "";
    public string Evidence { get; private set; } = "{}";
    public string SourceVersion { get; private set; } = "";
    public DateOnly SourceSnapshotDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}
