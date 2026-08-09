using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Cart.AddOfferToCart;

public sealed class AddOfferToCartCommandHandler(
    ICartRepository carts, IOnlineOfferEligibilityService eligibility, IMediator mediator)
    : IRequestHandler<AddOfferToCartCommand, AddOfferToCartResponse>
{
    public async Task<AddOfferToCartResponse> Handle(AddOfferToCartCommand request, CancellationToken ct)
    {
        var context = await eligibility.ResolveByIdAsync(request.OfferId, request.Quantity,
            request.ProductVariantId, request.ProductSessionId, request.UsageDate, ct);
        var cart = await carts.AddOfferItemAsync(request.UserId, request.ModuleId,
            context.Shop.Id, context.Product.Id, context.Offer.Id,
            context.Offer.ProductVariantId, context.Offer.ProductSessionId, context.UsageDate,
            request.Quantity, context.Offer.OriginalPriceMinor, context.Offer.FinalPriceMinor, ct);
        var projection = await mediator.Send(new GetOfferCartQuery(request.UserId, request.ModuleId), ct)
            ?? throw new InvalidOperationException("سبد خرید پس از افزودن آیتم قابل بازیابی نیست");
        return new AddOfferToCartResponse(cart.Id, cart.Items.Sum(x => x.Quantity), projection);
    }
}
