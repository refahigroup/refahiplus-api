namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelRoomsResponse
{
    public int hotel_id { get; set; }
    public List<SnappTripRoomItem> rooms { get; set; } = new();
}

public sealed class SnappTripRoomItem
{
    public int id { get; set; }
    public string title { get; set; } = default!;
    public string accommodation_type { get; set; } = default!;
    public int hotel_id { get; set; }
    public string board_type { get; set; } = default!;
    public int extra_bed { get; set; }
    public int adults { get; set; }
    public int children { get; set; }

    public string description { get; set; } = default!;

    public List<SnappTripRoomFacilityTag> facilities_tags { get; set; } = new();
}

public sealed class SnappTripRoomFacilityTag
{
    public int hotel_id { get; set; }
    public List<SnappTripFacility> facilities { get; set; } = new();
}

public sealed class SnappTripFacility
{
    public string title { get; set; } = default!;
    public string icon { get; set; } = default!;
}