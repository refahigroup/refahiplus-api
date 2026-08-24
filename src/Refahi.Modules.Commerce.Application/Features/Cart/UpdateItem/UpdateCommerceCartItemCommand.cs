using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Cart.UpdateItem;

public sealed record UpdateCommerceCartItemCommand(
    Guid UserId, 
    Guid ItemId, 
    int Quantity
) : IRequest<CommerceCartDto>;

