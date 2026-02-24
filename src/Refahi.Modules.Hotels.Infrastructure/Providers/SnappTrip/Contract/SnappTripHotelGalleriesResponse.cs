
namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelGalleriesResponse
{
    public int hotel_id { get; set; }
    public List<SnappTripHotelGalleriesResponseItem> gallery { get; set; } = new();
}

public sealed class SnappTripHotelGalleriesResponseItem
{
    public string url { get; set; }
    public string title { get; set; } = default!;
    public string description { get; set; } = default!;

}