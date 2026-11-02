namespace Refahi.Modules.Entertainments.Application.Features.Cart;

public sealed record EntertainmentCartItemDto(
    Guid Id, 
    string ProviderKey, 
    string SupplierKey, 
    string ProgramKey,
    string OfferKey, 
    string PurchaseOptionKey, 
    string Title,
    string SupplierTitle,
    string ProgramTitle,
    string? ProgramImageUrl,
    string OptionTitle,
    string OfferTitle,
    int Quantity, 
    long UnitPriceMinor,
    long OriginalUnitPriceMinor,
    bool IsAvailable
);


