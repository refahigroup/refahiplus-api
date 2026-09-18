using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// Trusted server-side automatic payment path. Allocation is resolved by Orders;
/// callers cannot choose payer Wallets.
/// </summary>
public sealed record ExecuteOrderPaymentCommand(
    Guid OrderId,
    Guid UserId,
    string IdempotencyKey,
    Guid DestinationWalletId)
    : IRequest<ExecuteOrderPaymentResponse>;

public sealed record ExecuteOrderPaymentResponse(
    Guid OrderId,
    string ExecutionStatus,
    Guid? PaymentId,
    DateTimeOffset? PaidAt,
    string? FailureCode = null);
