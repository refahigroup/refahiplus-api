namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceSellerDto(
    string ProviderKey, 
    string SellerKey, 
    string Title, 
    string? ImageUrl
);
