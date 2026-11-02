using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Exceptions;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.UpdateItem;

public sealed class UpdateEntertainmentCartItemCommandHandler(IEntertainmentRepository repository, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    IRequestHandler<UpdateEntertainmentCartItemCommand, EntertainmentCartDto>
{
    public async Task<EntertainmentCartDto> Handle(UpdateEntertainmentCartItemCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var cart = await Helpers.RequiredCart(repository, request.UserId, ct);

        cart.UpdateQuantity(request.ItemId, request.Quantity);

        await repository.SaveChangesAsync(ct);

        return Mapper.Map(cart);
    }
}
