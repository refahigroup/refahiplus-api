namespace Refahi.Modules.PaymentGateway.Domain.Enums;

public enum PaymentSessionStatus
{
    Initiated = 1,
    TokenReceived = 2,
    // Kept for historical rows. New sessions do not persist this presentation-only state.
    Redirected = 3,
    CallbackReceived = 4,
    Succeeded = 5,
    Failed = 6,
    Expired = 7,
    Processing = 8,
    PendingRecovery = 9,
}
