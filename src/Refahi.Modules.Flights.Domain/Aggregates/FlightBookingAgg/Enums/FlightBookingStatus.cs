namespace Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.Enums;

public enum FlightBookingStatus
{
    Draft = 0,
    ProviderBooked = 1,
    OrderCreated = 2,
    PaymentPending = 3,
    Paid = 4,
    Issuing = 5,
    Issued = 6,
    IssueFailed = 7,
    Expired = 8,
    CancellationQuoted = 9,
    CancellationRequested = 10,
    Cancelled = 11,
    CancellationFailed = 12,
    RefundPending = 13,
    Refunded = 14
}
