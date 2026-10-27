using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.RemoveItem;

public sealed record RemoveEntertainmentCartItemCommand(
    Guid UserId,
    Guid ItemId
) : IRequest<EntertainmentCartDto>;

