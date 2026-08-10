using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;

namespace Refahi.Modules.Store.Application.Contracts.Commands.Cart;

public sealed record AddOfferToCartCommand(
    Guid UserId,
    int ModuleId,
    Guid OfferId,
    int Quantity,
    Guid? ProductVariantId = null,
    Guid? ProductSessionId = null,
    DateOnly? UsageDate = null
) : IRequest<AddOfferToCartResponse>;

public sealed record AddOfferToCartResponse(Guid CartId, int TotalItems, OfferCartDto Cart);
