using MediatR;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.Clear;

public sealed class ClearEntertainmentCartCommandHandler(IEntertainmentRepository repository, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    IRequestHandler<ClearEntertainmentCartCommand>
{
    public async Task<Unit> Handle(ClearEntertainmentCartCommand request, CancellationToken ct)
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
