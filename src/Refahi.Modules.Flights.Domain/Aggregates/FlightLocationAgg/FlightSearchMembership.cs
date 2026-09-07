namespace Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;

public sealed class FlightSearchMembership
{
    private FlightSearchMembership() { }
    public string CityCode { get; private set; } = "";
    public string AirportCode { get; private set; } = "";
    public bool IsDomestic { get; private set; }
    public int CityRank { get; private set; }
    public int AirportRank { get; private set; }

    public static FlightSearchMembership Create(string cityCode, string airportCode, bool isDomestic,
        int cityRank, int airportRank) => new()
    {
        CityCode = cityCode, AirportCode = airportCode, IsDomestic = isDomestic,
        CityRank = cityRank, AirportRank = airportRank
    };
}

