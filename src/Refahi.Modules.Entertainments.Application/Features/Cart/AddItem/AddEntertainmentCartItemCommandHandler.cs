using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Exceptions;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Cart.AddItem;

public sealed class AddEntertainmentCartItemCommandHandler(IEntertainmentRepository repository, IEntertainmentProviderFactory providers, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    IRequestHandler<AddEntertainmentCartItemCommand, EntertainmentCartDto>
{
    public async Task<EntertainmentCartDto> Handle(AddEntertainmentCartItemCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var quote = await providers.GetRequired(request.ProviderKey)
                                   .QuoteAsync(new(request.ProgramKey, request.OfferKey, request.PurchaseOptionKey, request.Quantity), ct);

        if (quote.UnitPriceMinor != request.ExpectedUnitPriceMinor)
            throw new EntertainmentPriceChangedException(quote);

        var cart = await repository.GetCartAsync(request.UserId, ct);

        if (cart is null)
        {
            cart = EntertainmentCart.Create(request.UserId);
            await repository.AddCartAsync(cart, ct);
        }

        cart.AddOrReplace(new(
            quote.ProviderKey,
            quote.SupplierKey,
            quote.ProgramKey,
            quote.OfferKey,
            quote.PurchaseOptionKey,
            $"{quote.ProgramTitle} - {quote.OptionTitle}",
            quote.SupplierTitle,
            quote.ProgramTitle,
            quote.ProgramImageUrl,
            quote.OptionTitle,
            quote.OfferTitle,
            request.Quantity,
            quote.UnitPriceMinor,
            quote.OriginalUnitPriceMinor
        ));

        await repository.SaveChangesAsync(ct); return Mapper.Map(cart);
    }

}
