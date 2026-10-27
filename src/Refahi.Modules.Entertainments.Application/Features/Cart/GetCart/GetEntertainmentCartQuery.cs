using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.GetCart;

public sealed record GetEntertainmentCartQuery(
    Guid UserId
) : IRequest<EntertainmentCartDto>;

