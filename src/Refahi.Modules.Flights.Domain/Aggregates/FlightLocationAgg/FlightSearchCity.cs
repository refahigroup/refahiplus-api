namespace Refahi.Modules.Flights.Domain.Aggregates.FlightLocationAgg;

public sealed class FlightSearchCity
{
    private FlightSearchCity() { }
    public string CityCode { get; private set; } = "";
    public string CityNameFa { get; private set; } = "";
    public string CityNameEn { get; private set; } = "";
    public string CountryCode { get; private set; } = "";
    public string CountryNameFa { get; private set; } = "";
    public string CountryNameEn { get; private set; } = "";

    public static FlightSearchCity Create(string cityCode, string cityNameFa, string cityNameEn,
        string countryCode, string countryNameFa, string countryNameEn) => new()
    {
        CityCode = cityCode, CityNameFa = cityNameFa, CityNameEn = cityNameEn,
        CountryCode = countryCode, CountryNameFa = countryNameFa, CountryNameEn = countryNameEn
    };
}

