namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripBookingStatusResponse
{
    public string reservation_code { get; set; } = default!;
    public int hotel_id { get; set; }
    public string checkin { get; set; } = default!;
    public string checkout { get; set; } = default!;
    public int price { get; set; }
    public string state { get; set; } = default!;

    public List<SnappTripBookedRoom> rooms { get; set; } = new();
}

public sealed class SnappTripBookedRoom
{
    public int room_id { get; set; }
    public int adults { get; set; }
    public int children { get; set; }
    public int infants { get; set; }
}
