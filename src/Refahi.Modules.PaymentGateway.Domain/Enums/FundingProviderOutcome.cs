namespace Refahi.Modules.PaymentGateway.Domain.Enums;

public enum FundingProviderOutcome
{
    VerifiedSuccess = 1,
    DefinitiveFailure = 2,
    Pending = 3,
    Unknown = 4,
}
