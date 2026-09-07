using MediatR;

namespace Refahi.Modules.Commerce.Application.Features.Cart.AddItem;

public sealed record AddCommerceCartItemCommand(
    Guid UserId, 
    string ProviderKey, 
    string SellerKey, 
    string ProductKey,
    string OfferKey, 
    string PurchaseOptionKey, 
    int Quantity, 
    long ExpectedUnitPriceMinor
) : IRequest<CommerceCartDto>;

