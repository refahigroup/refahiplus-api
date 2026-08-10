namespace Refahi.Modules.Store.Domain.Enums;

public enum StoreOrderStatus : short
{
    PendingOrder = 1,
    PendingPayment = 2,
    Paid = 3,
    Cancelled = 4,
    Refunded = 5,
    Failed = 6,
}
