namespace Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.StartPayment;

public sealed class StartHotelBookingPaymentResponse
{
    public Guid BookingId { get; set; }
    public long AmountMinor { get; set; }
}

