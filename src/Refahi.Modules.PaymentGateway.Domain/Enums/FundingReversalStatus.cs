namespace Refahi.Modules.PaymentGateway.Domain.Enums;

public enum FundingReversalStatus : short
{
    None = 0,
    Required = 1,
    Pending = 2,
    Succeeded = 3,
    Failed = 4,
    Unsupported = 5,
}
