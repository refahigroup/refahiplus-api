using MediatR;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.Clear;

public sealed class ClearCommerceCartCommandHandler(ICommerceRepository repository, Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceMutationLock gate) :
    IRequestHandler<ClearCommerceCartCommand>
{
    public async Task<Unit> Handle(ClearCommerceCartCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var cart = await repository.GetCartAsync(request.UserId, ct);

        if (cart is not null)
        {
            await repository.DeleteCartAsync(cart, ct);
            await repository.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
