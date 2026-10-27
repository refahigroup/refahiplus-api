using MediatR;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.UpdateItem;

public sealed record UpdateEntertainmentCartItemCommand(
    Guid UserId, 
    Guid ItemId, 
    int Quantity
) : IRequest<EntertainmentCartDto>;

