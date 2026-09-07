namespace Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

public sealed record CommerceSellerDto(
    string ProviderKey, 
    string SellerKey, 
    string Title, 
    IEnumerable<string> Categories,
    string? LogoUrl,
    string? CoverUrl,
    string? Description,
    CommerceAddressDto Address
);