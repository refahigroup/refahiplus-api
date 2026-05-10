namespace Refahi.Modules.PaymentGateway.Domain.Enums;

public enum PaymentSessionStatus
{
    Initiated = 1,
    TokenReceived = 2,
    Redirected = 3,
    CallbackReceived = 4,
    Succeeded = 5,
    Failed = 6,
    Expired = 7
}
