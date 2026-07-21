namespace Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;

public sealed class FlightAirport
{
    private FlightAirport()
    {
    }

    private FlightAirport(
        string iataCode,
        string? icaoCode,
        string cityCode,
        string airportNameFa,
        string airportNameEn,
        string cityNameFa,
        string cityNameEn,
        string countryCode,
        string countryNameFa,
        string countryNameEn,
        decimal? latitude,
        decimal? longitude,
        bool isPopular,
        string sourceVersion,
        string translationSource,
        string searchText,
        DateTime importedAtUtc)
    {
        IataCode = iataCode;
        Apply(
            icaoCode,
            cityCode,
            airportNameFa,
            airportNameEn,
            cityNameFa,
            cityNameEn,
            countryCode,
            countryNameFa,
            countryNameEn,
            latitude,
            longitude,
            isPopular,
            sourceVersion,
            translationSource,
            searchText,
            importedAtUtc);
    }

    public string IataCode { get; private set; } = string.Empty;
    public string? IcaoCode { get; private set; }
    public string CityCode { get; private set; } = string.Empty;
    public string AirportNameFa { get; private set; } = string.Empty;
    public string AirportNameEn { get; private set; } = string.Empty;
    public string CityNameFa { get; private set; } = string.Empty;
    public string CityNameEn { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string CountryNameFa { get; private set; } = string.Empty;
    public string CountryNameEn { get; private set; } = string.Empty;
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public bool IsPopular { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string SourceVersion { get; private set; } = string.Empty;
    public string TranslationSource { get; private set; } = string.Empty;
    public string SearchText { get; private set; } = string.Empty;
    public DateTime ImportedAtUtc { get; private set; }

    public static FlightAirport Create(
        string iataCode,
        string? icaoCode,
        string cityCode,
        string airportNameFa,
        string airportNameEn,
        string cityNameFa,
        string cityNameEn,
        string countryCode,
        string countryNameFa,
        string countryNameEn,
        decimal? latitude,
        decimal? longitude,
        bool isPopular,
        string sourceVersion,
        string translationSource,
        string searchText,
        DateTime importedAtUtc)
        => new(
            NormalizeCode(iataCode, 3),
            string.IsNullOrWhiteSpace(icaoCode) ? null : NormalizeCode(icaoCode, 4),
            NormalizeCode(cityCode, 3),
            Required(airportNameFa, nameof(airportNameFa)),
            Required(airportNameEn, nameof(airportNameEn)),
            Required(cityNameFa, nameof(cityNameFa)),
            Required(cityNameEn, nameof(cityNameEn)),
            NormalizeCode(countryCode, 2),
            Required(countryNameFa, nameof(countryNameFa)),
            Required(countryNameEn, nameof(countryNameEn)),
            latitude,
            longitude,
            isPopular,
            Required(sourceVersion, nameof(sourceVersion)),
            Required(translationSource, nameof(translationSource)),
            Required(searchText, nameof(searchText)),
            importedAtUtc);

    public void RefreshFromSource(
        string? icaoCode,
        string cityCode,
        string airportNameFa,
        string airportNameEn,
        string cityNameFa,
        string cityNameEn,
        string countryCode,
        string countryNameFa,
        string countryNameEn,
        decimal? latitude,
        decimal? longitude,
        bool isPopular,
        string sourceVersion,
        string translationSource,
        string searchText,
        DateTime importedAtUtc)
        => Apply(
            string.IsNullOrWhiteSpace(icaoCode) ? null : NormalizeCode(icaoCode, 4),
            NormalizeCode(cityCode, 3),
            Required(airportNameFa, nameof(airportNameFa)),
            Required(airportNameEn, nameof(airportNameEn)),
            Required(cityNameFa, nameof(cityNameFa)),
            Required(cityNameEn, nameof(cityNameEn)),
            NormalizeCode(countryCode, 2),
            Required(countryNameFa, nameof(countryNameFa)),
            Required(countryNameEn, nameof(countryNameEn)),
            latitude,
            longitude,
            isPopular,
            Required(sourceVersion, nameof(sourceVersion)),
            Required(translationSource, nameof(translationSource)),
            Required(searchText, nameof(searchText)),
            importedAtUtc);

    public void Deactivate() => IsActive = false;

    private void Apply(
        string? icaoCode,
        string cityCode,
        string airportNameFa,
        string airportNameEn,
        string cityNameFa,
        string cityNameEn,
        string countryCode,
        string countryNameFa,
        string countryNameEn,
        decimal? latitude,
        decimal? longitude,
        bool isPopular,
        string sourceVersion,
        string translationSource,
        string searchText,
        DateTime importedAtUtc)
    {
        IcaoCode = icaoCode;
        CityCode = cityCode;
        AirportNameFa = airportNameFa;
        AirportNameEn = airportNameEn;
        CityNameFa = cityNameFa;
        CityNameEn = cityNameEn;
        CountryCode = countryCode;
        CountryNameFa = countryNameFa;
        CountryNameEn = countryNameEn;
        Latitude = latitude;
        Longitude = longitude;
        IsPopular = isPopular;
        IsActive = true;
        SourceVersion = sourceVersion;
        TranslationSource = translationSource;
        SearchText = searchText;
        ImportedAtUtc = importedAtUtc;
    }

    private static string NormalizeCode(string value, int length)
    {
        var normalized = Required(value, nameof(value)).ToUpperInvariant();
        if (normalized.Length != length)
            throw new ArgumentException($"Code must contain {length} characters.", nameof(value));

        return normalized;
    }

    private static string Required(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", parameterName);

        return value.Trim();
    }
}
