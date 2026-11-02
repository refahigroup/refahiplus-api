namespace Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

// Reviewed mappings are maintained by the geography scripts, never by discovery requests.
public sealed class SnappTripCityMapping
{
    private SnappTripCityMapping() { }
    public string ProviderCityId { get; private set; } = "";
    public int CityId { get; private set; }
}

public sealed class LamasoCityMapping
{
    private LamasoCityMapping() { }
    public string ProviderCityId { get; private set; } = "";
    public int CityId { get; private set; }
    public bool IsEnabled { get; private set; }
}
