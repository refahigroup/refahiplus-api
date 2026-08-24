using MediatR;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.Clear;

public sealed class ClearCommerceCartCommandHandler(ICommerceRepository repository) :
    IRequestHandler<ClearCommerceCartCommand>
{
    public async Task<Unit> Handle(ClearCommerceCartCommand request, CancellationToken ct)
    {
        var cart = await repository.GetCartAsync(request.UserId, ct);

        if (cart is not null)
        {
            await repository.DeleteCartAsync(cart, ct);
            await repository.SaveChangesAsync(ct);
        }

        return Unit.Value;
    }
}
