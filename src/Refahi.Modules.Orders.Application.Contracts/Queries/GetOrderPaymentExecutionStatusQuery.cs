using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public sealed record GetOrderPaymentExecutionStatusQuery(Guid OrderId, Guid UserId)
    : IRequest<OrderPaymentExecutionStatusDto?>;

public sealed record OrderPaymentExecutionStatusDto(
    Guid OrderId,
    Guid UserId,
    long AmountMinor,
    string Currency,
    string SourceModule,
    Guid? SourceReferenceId,
    string ReferenceType,
    string CategoryCode,
    string ExecutionStatus,
    Guid? PaymentIntentId,
    Guid? PaymentId,
    DateTimeOffset? PaidAt);
