using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Exceptions;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.RemoveItem;

public sealed class RemoveEntertainmentCartItemCommandHandler(IEntertainmentRepository repository, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    IRequestHandler<RemoveEntertainmentCartItemCommand, EntertainmentCartDto>
{
    public async Task<EntertainmentCartDto> Handle(RemoveEntertainmentCartItemCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var cart = await Helpers.RequiredCart(repository, request.UserId, ct); 

        cart.Remove(request.ItemId); 

        await repository.SaveChangesAsync(ct); 

        return Mapper.Map(cart); 
    }
}
