namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripHotelCalendarResponse
{
    public int id { get; set; }
    public string from { get; set; } = default!;
    public string to { get; set; } = default!;
    public List<SnappTripBriefRack> racks { get; set; } = new();
    public List<SnappTripRoomCalendarData> rooms { get; set; } = new();
}

public sealed class SnappTripBriefRack
{
    public string checkin { get; set; } = default!;
    public string checkout { get; set; } = default!;
    public List<int> roomIDs { get; set; } = new();
}

public sealed class SnappTripRoomCalendarData
{
    public int id { get; set; }
    public string name { get; set; } = default!;
    public Dictionary<string, SnappTripDailyCalendar> daily { get; set; } = new();
}

public sealed class SnappTripDailyCalendar
{
    public string date { get; set; } = default!;
    public int availability { get; set; }
    public int price { get; set; }
    public int original_sell_price { get; set; }
    public int discount_amount { get; set; }
    public int child_price { get; set; }
    public int extra_bed_price { get; set; }
    public int min_stay { get; set; }
}
