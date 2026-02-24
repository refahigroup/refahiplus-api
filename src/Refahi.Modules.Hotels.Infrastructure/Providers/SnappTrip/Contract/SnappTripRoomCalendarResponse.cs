namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripRoomCalendarDay
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
