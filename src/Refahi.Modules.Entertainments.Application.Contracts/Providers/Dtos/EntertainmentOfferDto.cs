namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

public sealed record EntertainmentOfferDto(
    string ProviderKey, 
    string SupplierKey, 
    string ProgramKey, 
    string OfferKey,
    string Title, 
    string? Subtitle,
    DateTimeOffset? StartsAt, 
    int? Capacity, 
    string CategoryCode,
    IReadOnlyList<EntertainmentPurchaseOptionDto> PurchaseOptions
);
