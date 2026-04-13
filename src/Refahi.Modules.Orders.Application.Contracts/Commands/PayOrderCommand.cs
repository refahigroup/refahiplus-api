using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// پرداخت سفارش از کیف‌پول — Reserve + Capture
/// </summary>
public sealed record PayOrderCommand(
    Guid OrderId,
    List<WalletAllocationInput> Allocations,
    string IdempotencyKey
) : IRequest<PayOrderResponse>;

public sealed record WalletAllocationInput(Guid WalletId, long AmountMinor);

public sealed record PayOrderResponse(
    Guid OrderId,
    Guid PaymentId,
    string Status);
