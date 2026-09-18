using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class PaymentTerminationOptions
{
    public const string SectionName = "CreditPayments:PaymentTermination";
    public int ExpirationPollSeconds { get; set; } = 30;
    public int ExpirationBatchSize { get; set; } = 50;
}

public sealed record CancelGatewayPaymentCommand(
    Guid MerchantId,
    Guid TerminalId,
    Guid? CredentialId,
    Guid PaymentId,
    string IdempotencyKey,
    string? Reason) : IRequest<GatewayPaymentInquiryDto>;

public sealed record CancelCheckoutPaymentCommand(
    string Token,
    string? CheckoutAuthorizationSecret,
    string IdempotencyKey,
    string? Reason) : IRequest<CheckoutPaymentExecutionDto>;

public sealed record ResolvePaymentTerminationCommand(Guid GatewayPaymentId) : IRequest<Unit>;
public sealed record ProcessDueExpirationsCommand : IRequest<Unit>;

public sealed record CancelPaymentRequest(string? Reason);
