namespace Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg.Enums;

public enum HotelBookingSagaStatus
{
    Started = 1,
    RequestCreated = 2,
    OrderCreated = 3,
    PaymentPending = 4,
    Paid = 5,
    ProviderBookingStarted = 6,
    ProviderBookingConfirmed = 7,
    Completed = 8,
    Failed = 9,
    Compensated = 10
}
