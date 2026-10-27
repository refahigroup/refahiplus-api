using MediatR;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.GetCart;

public sealed class GetEntertainmentCartQueryHandler(IEntertainmentRepository repository) :
    IRequestHandler<GetEntertainmentCartQuery, EntertainmentCartDto>
{
    public async Task<EntertainmentCartDto> Handle(GetEntertainmentCartQuery request, CancellationToken ct)
    {
        return Mapper.Map(await repository.GetCartAsync(request.UserId, ct));
    }
}
