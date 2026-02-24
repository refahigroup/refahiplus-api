namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;


public sealed class SnappTripAvailabilityResponse
{
    public int hotel_id { get; set; }
    public List<SnappTripRoomAvailability> availability { get; set; } = new();
}

public sealed class SnappTripRoomAvailability
{
    public int availability { get; set; }
    public string from { get; set; } = default!;
    public string to { get; set; } = default!;
    public int min_stay { get; set; }

    public SnappTripRoomPricing pricing { get; set; } = new();
    public SnappTripRoomRacks racks { get; set; } = new();

    public SnappTripRoom room { get; set; } = new();
}

public sealed class SnappTripRoomPricing
{
    public int discount_amount { get; set; }
    public int child_price { get; set; }
    public int extra_bed_price { get; set; }
    public int original_sell_price { get; set; }
    public int price { get; set; }
}

public sealed class SnappTripRoomRacks
{
    public string title { get; set; } = default!;
    public List<SnappTripRack> racks { get; set; } = new();
}

public sealed class SnappTripRack
{
    public string checkin { get; set; } = default!;
    public string checkout { get; set; } = default!;
}

public sealed class SnappTripRoom
{
    public int id { get; set; }
    public string title { get; set; } = default!;
    public int adults { get; set; }
    public int children { get; set; }
    public string description { get; set; } = default!;
    public string accommodation_type { get; set; } = default!;

    public int extra_bed { get; set; }

    public List<SnappTripRoomFacilityTag> facilities_tags { get; set; } = new();
}
