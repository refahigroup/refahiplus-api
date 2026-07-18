namespace Refahi.Modules.Charge.Domain.Enums;

public enum ProviderCallOutcome : short
{
    Success = 1,
    ProviderRejected = 2,
    Timeout = 3,
    TransportError = 4,
    AuthenticationError = 5,
    InvalidResponse = 6,
    Cancelled = 7
}
