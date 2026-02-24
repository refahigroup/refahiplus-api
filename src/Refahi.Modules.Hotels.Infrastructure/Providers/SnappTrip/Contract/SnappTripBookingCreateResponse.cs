namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

public sealed class SnappTripBookingCreateResponse
{
    public string reservation_code { get; set; } = default!;
    public int price { get; set; }
    public string state { get; set; } = default!;
}
