namespace Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

public sealed record EntertainmentSupplierDto(
    string ProviderKey, 
    string SupplierKey, 
    string Title, 
    IEnumerable<string> Categories,
    string? LogoUrl,
    string? CoverUrl,
    string? Description,
    EntertainmentAddressDto Address
);