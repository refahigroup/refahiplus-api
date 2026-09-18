using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class PaymentExecutionOptions
{
    public const string SectionName = "CreditPayments:PaymentExecution";
    public int RecoveryPollSeconds { get; set; } = 30;
    public int AutoResumePollSeconds { get; set; } = 15;
    public int RecoveryBatchSize { get; set; } = 50;
    public int ProcessingLeaseSeconds { get; set; } = 120;
    public int BaseRetrySeconds { get; set; } = 30;
    public int MaximumRetrySeconds { get; set; } = 900;
}

public sealed class MerchantPaymentPolicyOptions
{
    public const string SectionName = "CreditPayments:MerchantPaymentPolicy";
    public List<MerchantPaymentPolicyEntry> Policies { get; set; } = [];
    public string FinanceApprovalReference { get; set; } = string.Empty;
}

public sealed class MerchantPaymentPolicyEntry
{
    public Guid? MerchantId { get; set; }
    public Guid? TerminalId { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public Guid DestinationWalletId { get; set; }
    public DateTimeOffset EffectiveAt { get; set; }
}

public sealed record ConfirmCheckoutPaymentCommand(
    string Token,
    string? CheckoutAuthorizationSecret)
    : IRequest<CheckoutPaymentExecutionDto>;

public sealed record ExecuteGatewayPaymentCommand(
    Guid GatewayPaymentId,
    string Trigger)
    : IRequest<CheckoutPaymentExecutionDto>;

public sealed record RecoverGatewayPaymentCommand(Guid PaymentAttemptId) : IRequest<Unit>;

public sealed record CheckoutPaymentExecutionDto(
    string Status,
    long AmountMinor,
    string Currency,
    DateTimeOffset? PaidAt,
    string? ReturnUrl,
    CheckoutFundingDto? Funding = null,
    string? OutcomeCode = null);

public sealed record MerchantPaymentPolicy(
    string PolicyId,
    string PolicyVersion,
    string PolicyHash,
    DateTimeOffset EffectiveAt,
    string CategoryCode,
    Guid DestinationWalletId);

public interface IMerchantPaymentPolicyResolver
{
    Task<MerchantPaymentPolicy?> ResolveAsync(
        Guid merchantId,
        Guid terminalId,
        string approvedCategoryCode,
        long amountMinor,
        string currency,
        CancellationToken ct);
}
