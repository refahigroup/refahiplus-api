using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Cart.RemoveItem;

public sealed record RemoveCommerceCartItemCommand(
    Guid UserId,
    Guid ItemId
) : IRequest<CommerceCartDto>;

