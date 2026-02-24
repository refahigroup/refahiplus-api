namespace Refahi.Modules.Hotels.Domain.Aggregates.BookingAgg.Enums;

public enum BookingStatus
{
    Draft = 0,
    Provisional = 1,
    PaymentPending = 2,
    PaymentFailed = 3,
    ConfirmingProvider = 4,
    Confirmed = 5,
    ConfirmFailed = 6,
    ProviderFailed = 7,
    Expired = 8
}
