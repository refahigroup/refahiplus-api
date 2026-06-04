namespace Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.GetBookingDetails;

public sealed class HotelBookingDetailsResponse
{
    public Guid BookingId { get; set; }
    public long HotelId { get; set; }
    public long RoomId { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int RoomsCount { get; set; }
    public string BoardType { get; set; } = "";
    public string Status { get; set; } = "";
    public long CustomerPrice { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public List<HotelBookingGuestResponse> Guests { get; set; } = [];
    public HotelBookingVoucherResponse? Voucher { get; set; }
}

public sealed class HotelBookingGuestResponse
{
    public string FullName { get; set; } = "";
    public int Age { get; set; }
    public string Type { get; set; } = "";
}

public sealed class HotelBookingVoucherResponse
{
    public string Code { get; set; } = "";
}

