using MediatR;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.GetCart;

public sealed class GetCommerceCartQueryHandler(ICommerceRepository repository) :
    IRequestHandler<GetCommerceCartQuery, CommerceCartDto>
{
    public async Task<CommerceCartDto> Handle(GetCommerceCartQuery request, CancellationToken ct)
    {
        return Mapper.Map(await repository.GetCartAsync(request.UserId, ct));
    }
}
