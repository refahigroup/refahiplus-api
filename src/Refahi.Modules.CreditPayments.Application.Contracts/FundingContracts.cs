using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed record EvaluateCheckoutFundingCommand(
    string Token,
    string? CheckoutAuthorizationSecret)
    : IRequest<CheckoutFundingEvaluationDto>;

public sealed record CheckoutFundingEvaluationDto(
    Guid PaymentId,
    string PaymentStatus,
    long AmountMinor,
    string Currency,
    CheckoutFundingDto Funding);

public sealed record CheckoutFundingDto(
    bool IsSufficient,
    long EligibleAmountMinor,
    long DeficitAmountMinor,
    bool CanOfferWalletTopUp,
    DateTimeOffset EvaluatedAt);

public sealed class FundingContinuationOptions
{
    public const string SectionName = "CreditPayments:FundingContinuation";
    public int MaximumAttemptsPerPayment { get; set; } = 3;
    public int RecoveryPollSeconds { get; set; } = 30;
    public int RecoveryBatchSize { get; set; } = 50;
    public int ProcessingLeaseSeconds { get; set; } = 120;
    public int BaseRetrySeconds { get; set; } = 30;
    public int MaximumRetrySeconds { get; set; } = 900;
}

public sealed record StartCheckoutFundingCommand(
    string Token,
    string? CheckoutAuthorizationSecret)
    : IRequest<CheckoutFundingContinuationDto>;

public sealed record ReconcileCheckoutFundingCommand(
    string Token,
    string? CheckoutAuthorizationSecret)
    : IRequest<CheckoutFundingContinuationDto>;

public sealed record RecoverFundingContinuationCommand(Guid FundingContinuationId)
    : IRequest<Unit>;

public sealed record CheckoutFundingContinuationDto(
    bool FundingRequired,
    string Status,
    long? AmountMinor,
    string Currency,
    string? RedirectUrl,
    long? CurrentDeficitMinor,
    bool ReadyForAutomaticResume);
