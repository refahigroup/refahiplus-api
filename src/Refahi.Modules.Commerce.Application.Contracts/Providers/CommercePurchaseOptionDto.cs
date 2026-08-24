namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommercePurchaseOptionDto(
    string Key, 
    string Title, 
    long PriceMinor, 
    long? OriginalPriceMinor, 
    bool IsAvailable
);
