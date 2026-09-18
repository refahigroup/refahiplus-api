using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class RefundOptions
{
    public const string SectionName = "CreditPayments:Refund";
    public int RecoveryPollSeconds { get; set; } = 15;
    public int LeaseSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public int MaximumBackoffSeconds { get; set; } = 900;
}

public sealed record CreateRefundCommand(Guid MerchantId, Guid TerminalId, Guid CredentialId,
    Guid PaymentId, string IdempotencyKey, long AmountMinor,
    string? MerchantRefundReference, string? Reason) : IRequest<RefundDto>;
public sealed record GetRefundQuery(Guid MerchantId, Guid TerminalId, Guid RefundId) : IRequest<RefundDto?>;
public sealed record ListRefundsQuery(Guid MerchantId, Guid TerminalId, Guid PaymentId) : IRequest<IReadOnlyList<RefundDto>?>;
public sealed record ProcessRefundCommand(Guid RefundId) : IRequest<RefundDto?>;

public sealed record RefundDto(Guid RefundId, Guid PaymentId, string? MerchantRefundReference,
    string Status, long AmountMinor, string Currency, string? Reason,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
