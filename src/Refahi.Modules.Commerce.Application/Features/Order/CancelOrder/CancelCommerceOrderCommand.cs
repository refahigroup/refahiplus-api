using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Order.CancelOrder;

public sealed record CancelCommerceOrderCommand(
    Guid UserId, 
    string CallerRole, 
    Guid CommerceOrderId, 
    string? Reason,
    string IdempotencyKey
) : IRequest<CommerceOrderDto>;