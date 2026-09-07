namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

public sealed record CommerceOfferDto(
    string ProviderKey, 
    string SellerKey, 
    string ProductKey, 
    string OfferKey,
    string Title, 
    string? Subtitle,
    DateTimeOffset? StartsAt, 
    int? Capacity, 
    string CategoryCode,
    IReadOnlyList<CommercePurchaseOptionDto> PurchaseOptions
);
