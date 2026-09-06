using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Exceptions;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Cart.AddItem;

public sealed class AddCommerceCartItemCommandHandler(ICommerceRepository repository, ICommerceProviderFactory providers, Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceMutationLock gate) :
    IRequestHandler<AddCommerceCartItemCommand, CommerceCartDto>
{
    public async Task<CommerceCartDto> Handle(AddCommerceCartItemCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var quote = await providers.GetRequired(request.ProviderKey)
                                   .QuoteAsync(new(request.ProductKey, request.OfferKey, request.PurchaseOptionKey, request.Quantity), ct);

        if (quote.UnitPriceMinor != request.ExpectedUnitPriceMinor)
            throw new CommercePriceChangedException(quote);

        var cart = await repository.GetCartAsync(request.UserId, ct);

        if (cart is null)
        {
            cart = CommerceCart.Create(request.UserId);
            await repository.AddCartAsync(cart, ct);
        }

        cart.AddOrReplace(new(
            quote.ProviderKey,
            quote.SellerKey,
            quote.ProductKey,
            quote.OfferKey,
            quote.PurchaseOptionKey,
            $"{quote.ProductTitle} - {quote.OptionTitle}",
            quote.SellerTitle,
            quote.ProductTitle,
            quote.ProductImageUrl,
            quote.OptionTitle,
            quote.OfferTitle,
            request.Quantity,
            quote.UnitPriceMinor,
            quote.OriginalUnitPriceMinor
        ));

        await repository.SaveChangesAsync(ct); return Mapper.Map(cart);
    }

}
