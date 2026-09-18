using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

public sealed record TerminateOrderPaymentCommand(
    Guid OrderId,
    Guid UserId,
    string ReleaseReference) : IRequest<TerminateOrderPaymentResponse>;

public sealed record TerminateOrderPaymentResponse(
    Guid OrderId,
    string TerminationStatus,
    Guid? PaymentIntentId = null,
    Guid? PaymentId = null);
