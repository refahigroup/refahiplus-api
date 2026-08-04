using MediatR;

namespace Refahi.Modules.Store.Application.Contracts.Vendor;

public sealed record VendorShopSummaryDto(
    Guid Id,
    string Name,
    string Status,
    string ShopType,
    string? LogoUrl);

public sealed record GetVendorShopsBySupplierIdQuery(Guid SupplierId)
    : IRequest<IReadOnlyList<VendorShopSummaryDto>>;
