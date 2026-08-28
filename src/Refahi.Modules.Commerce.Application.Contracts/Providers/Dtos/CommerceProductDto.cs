namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

public sealed record CommerceProductDto(
    string ProviderKey, 
    string SellerKey, 
    string ProductKey,
    string Title,
    string? Description, 
    string? ImageUrl, 
    IEnumerable<string> Tags,
    CommerceAddressDto Address,
    IReadOnlyList<CommerceOfferDto> Offers
);
