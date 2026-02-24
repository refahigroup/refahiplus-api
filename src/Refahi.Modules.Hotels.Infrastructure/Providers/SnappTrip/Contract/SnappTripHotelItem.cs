namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public class SnappTripHotelItem
{
    public long hotel_id { get; set; }
    public string name { get; set; } = default!;
    public string city_name { get; set; } = default!;
    public string accommodation_type { get; set; } = default!;
    public int stars { get; set; }
    public long min_price { get; set; }
    public string thumbnail { get; set; } = default!;
}
