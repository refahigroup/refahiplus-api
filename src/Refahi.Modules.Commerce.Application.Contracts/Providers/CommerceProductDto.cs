namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceProductDto(
    string ProviderKey, 
    string SellerKey, 
    string ProductKey,
    string Title,
    string? Description, 
    string? ImageUrl, 
    IReadOnlyList<CommerceOfferDto> Offers
);
