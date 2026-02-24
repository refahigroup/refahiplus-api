namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripCityData
{
    public int id { get; set; }
    public string title_fa { get; set; } = default!;
    public string title_en { get; set; } = default!;
    public SnappTripStateData state { get; set; } = new();
}

public sealed class SnappTripStateData
{
    public int id { get; set; }
    public string title { get; set; } = default!;
}
