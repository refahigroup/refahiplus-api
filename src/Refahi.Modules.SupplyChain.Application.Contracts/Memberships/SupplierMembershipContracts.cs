using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Memberships;

public sealed record VendorBusinessProfileDto(
    Guid SupplierId,
    string SupplierName,
    string? BrandName,
    string? SupplierMobile,
    string? SupplierPhone,
    string? RepresentativeName,
    string? RepresentativePhone,
    IReadOnlyList<VendorBusinessShopDto> Shops
);

public sealed record VendorBusinessShopDto(
    Guid Id,
    string Name,
    string? ContactPhone,
    string? Address,
    string? LogoUrl,
    string? CoverImageUrl
);

public sealed record GetVendorBusinessProfileQuery(Guid UserId, Guid SupplierId)
    : IRequest<VendorBusinessProfileDto?>;

public sealed record UpdateVendorSupplierProfileCommand(
    Guid UserId,
    Guid SupplierId,
    string? BrandName,
    string? MobileNumber,
    string? PhoneNumber,
    string? RepresentativeName,
    string? RepresentativePhone
) : IRequest<VendorBusinessProfileDto?>;

public sealed record UpdateVendorShopProfileCommand(
    Guid UserId,
    Guid SupplierId,
    Guid ShopId,
    string Name,
    string? ContactPhone,
    string? Address,
    string? LogoUrl,
    string? CoverImageUrl
) : IRequest<VendorBusinessProfileDto?>;
