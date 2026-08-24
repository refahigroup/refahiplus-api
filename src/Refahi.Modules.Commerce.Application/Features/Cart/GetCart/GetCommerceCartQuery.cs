using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Cart.GetCart;

public sealed record GetCommerceCartQuery(
    Guid UserId
) : IRequest<CommerceCartDto>;

