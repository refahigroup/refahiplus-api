namespace Refahi.Modules.Commerce.Application.Features.Cart;

public sealed record CommerceCartItemDto(
    Guid Id, 
    string ProviderKey, 
    string SellerKey, 
    string ProductKey,
    string OfferKey, 
    string PurchaseOptionKey, 
    string Title,
    string SellerTitle,
    string ProductTitle,
    string? ProductImageUrl,
    string OptionTitle,
    string OfferTitle,
    int Quantity, 
    long UnitPriceMinor,
    long OriginalUnitPriceMinor,
    bool IsAvailable
);


