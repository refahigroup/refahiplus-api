using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record RemoveCartItemCommand(
    Guid UserId,
    Guid CartItemId
) : IRequest<RemoveCartItemResponse>;

public sealed record RemoveCartItemResponse(Guid CartItemId);
