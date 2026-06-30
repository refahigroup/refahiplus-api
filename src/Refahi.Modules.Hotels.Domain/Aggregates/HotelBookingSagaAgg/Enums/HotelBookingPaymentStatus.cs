namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

public enum HotelBookingPaymentStatus
{
    None = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4
}
