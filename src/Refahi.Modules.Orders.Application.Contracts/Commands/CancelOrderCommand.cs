using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// لغو سفارش — Release یا Refund بسته به PaymentState
/// </summary>
public sealed record CancelOrderCommand(
    Guid OrderId,
    string? Reason,
    string IdempotencyKey
) : IRequest<CancelOrderResponse>;

public sealed record CancelOrderResponse(Guid OrderId, string Status, string PaymentAction);
