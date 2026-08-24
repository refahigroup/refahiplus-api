using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.RemoveItem;

public sealed class RemoveCommerceCartItemCommandHandler(ICommerceRepository repository) :
    IRequestHandler<RemoveCommerceCartItemCommand, CommerceCartDto>
{
    public async Task<CommerceCartDto> Handle(RemoveCommerceCartItemCommand request, CancellationToken ct)
    {
        var cart = await Helpers.RequiredCart(repository, request.UserId, ct); 

        cart.Remove(request.ItemId); 

        await repository.SaveChangesAsync(ct); 

        return Mapper.Map(cart); 
    }
}
