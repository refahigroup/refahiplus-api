namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

public sealed record EntertainmentPurchaseOptionDto(
    string Key, 
    string Title, 
    long PriceMinor, 
    long? OriginalPriceMinor, 
    bool IsAvailable
);
