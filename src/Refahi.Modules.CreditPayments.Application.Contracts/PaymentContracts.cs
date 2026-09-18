using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed record CreateGatewayPaymentCommand(
    Guid MerchantId,
    Guid TerminalId,
    Guid? CredentialId,
    string IdempotencyKey,
    string MerchantOrderId,
    long AmountMinor,
    string Currency,
    string? Description,
    string ReturnUrl,
    int? ExpiresInSeconds,
    IReadOnlyDictionary<string, string>? Metadata) : IRequest<CreateGatewayPaymentResponse>;

public sealed record GetGatewayPaymentQuery(
    Guid MerchantId,
    Guid TerminalId,
    Guid PaymentId) : IRequest<GatewayPaymentInquiryDto?>;

public sealed record CreateGatewayPaymentResponse(
    Guid PaymentId,
    string MerchantOrderId,
    string Status,
    long AmountMinor,
    string Currency,
    string CheckoutUrl,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record GatewayPaymentInquiryDto(
    Guid PaymentId,
    string MerchantOrderId,
    string Status,
    long AmountMinor,
    string Currency,
    string? Description,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Metadata,
    long CapturedAmountMinor = 0,
    long RefundedAmountMinor = 0,
    DateTimeOffset? PaidAt = null);
