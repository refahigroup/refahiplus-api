namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelBrief
{
    public int id { get; set; }
    public string name { get; set; } = default!;
    public int city_id { get; set; }
    public string fa_url { get; set; } = default!;
}
