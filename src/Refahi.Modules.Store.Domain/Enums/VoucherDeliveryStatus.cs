namespace Refahi.Modules.Store.Domain.Enums;

public enum VoucherDeliveryStatus : short
{
    Pending = 1,
    Retry = 2,
    Sent = 3,
    DeadLetter = 4,
    SkippedNoRecipient = 5,
}
