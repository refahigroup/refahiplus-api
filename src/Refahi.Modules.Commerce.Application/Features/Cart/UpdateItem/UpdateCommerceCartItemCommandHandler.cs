using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.UpdateItem;

public sealed class UpdateCommerceCartItemCommandHandler(ICommerceRepository repository) :
    IRequestHandler<UpdateCommerceCartItemCommand, CommerceCartDto>
{
    public async Task<CommerceCartDto> Handle(UpdateCommerceCartItemCommand request, CancellationToken ct)
    {
        var cart = await Helpers.RequiredCart(repository, request.UserId, ct);

        cart.UpdateQuantity(request.ItemId, request.Quantity);

        await repository.SaveChangesAsync(ct);

        return Mapper.Map(cart);
    }
}
