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
)
{
    public int ProgramType { get; init; } = 2;
    public bool IsActive { get; init; } = true;
    public bool RequiresManifest { get; init; }
    public bool RequiresIdentityNumber { get; init; }
    public string? Rules { get; init; }
    public IReadOnlyList<string> Gallery { get; init; } = [];
    public string? LocationCode { get; init; }
    public string? ExternalCategoryCode { get; init; }
    public string? UnavailableReason { get; init; }
}
