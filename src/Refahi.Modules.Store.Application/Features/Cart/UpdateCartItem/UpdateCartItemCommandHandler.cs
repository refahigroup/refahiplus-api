using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.UpdateCartItem;

public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResponse>
{
    private readonly ICartRepository _cartRepo;

    public UpdateCartItemCommandHandler(ICartRepository cartRepo)
        => _cartRepo = cartRepo;

    public async Task<UpdateCartItemResponse> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartRepo.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? throw new StoreDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");

        var item = cart.Items.FirstOrDefault(i => i.Id == request.CartItemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");

        cart.UpdateItemQuantity(request.CartItemId, request.Quantity);

        await _cartRepo.UpdateAsync(cart, cancellationToken);

        return new UpdateCartItemResponse(item.Id, request.Quantity);
    }
}
