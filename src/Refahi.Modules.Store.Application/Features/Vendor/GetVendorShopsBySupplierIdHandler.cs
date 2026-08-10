using MediatR;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Vendor;

public sealed class GetVendorShopsBySupplierIdHandler(
    IShopRepository shops,
    IPathService pathService
) : IRequestHandler<GetVendorShopsBySupplierIdQuery, IReadOnlyList<VendorShopSummaryDto>>
{
    public async Task<IReadOnlyList<VendorShopSummaryDto>> Handle(
        GetVendorShopsBySupplierIdQuery request,
        CancellationToken ct
    )
    {
        var supplierShops = await shops.GetBySupplierIdAsync(request.SupplierId, ct);
        var result = new List<VendorShopSummaryDto>(supplierShops.Count);

        foreach (var shop in supplierShops)
        {
            result.Add(
                new VendorShopSummaryDto(
                    shop.Id,
                    shop.Name,
                    shop.Status.ToString(),
                    shop.ShopType.ToString(),
                    shop.LogoUrl is null ? null : pathService.MakeAbsoluteMediaUrl(shop.LogoUrl)
                )
            );
        }

        return result;
    }
}
