namespace Refahi.Modules.Charge.Domain.Enums;

public enum ChargeRequestStatus : short
{
    Created = 1,
    ConvertedToOrder = 2,
    Paid = 3,
    Processing = 4,
    ReconciliationPending = 5,
    Fulfilled = 6,
    Failed = 7,
    Refunding = 8,
    Refunded = 9,
    ManualReview = 10,
    Cancelled = 11,
    Expired = 12
}
