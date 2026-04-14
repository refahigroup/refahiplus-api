using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.RemoveCartItem;

public class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, RemoveCartItemResponse>
{
    private readonly ICartRepository _cartRepo;

    public RemoveCartItemCommandHandler(ICartRepository cartRepo)
        => _cartRepo = cartRepo;

    public async Task<RemoveCartItemResponse> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepo.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new StoreDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");

        cart.RemoveItem(request.CartItemId);

        await _cartRepo.UpdateAsync(cart, cancellationToken);

        return new RemoveCartItemResponse(request.CartItemId);
    }
}
