using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Order.GetOrder;

public sealed record GetCommerceOrderQuery(
    Guid UserId,
    string CallerRole,
    Guid CommerceOrderId,
    bool RevealTickets = false
) : IRequest<CommerceOrderDto?>;




