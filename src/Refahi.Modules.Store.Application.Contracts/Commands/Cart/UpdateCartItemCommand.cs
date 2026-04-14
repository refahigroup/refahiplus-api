using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record UpdateCartItemCommand(
    Guid UserId,
    Guid CartItemId,
    int Quantity
) : IRequest<UpdateCartItemResponse>;

public sealed record UpdateCartItemResponse(Guid CartItemId, int Quantity);
