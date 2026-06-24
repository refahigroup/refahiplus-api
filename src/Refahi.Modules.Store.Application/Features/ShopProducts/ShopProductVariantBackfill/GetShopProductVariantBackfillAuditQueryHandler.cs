using MediatR;
using Refahi.Modules.Store.Application.Contracts.Queries.ShopProducts;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariantBackfill;

public class GetShopProductVariantBackfillAuditQueryHandler
    : IRequestHandler<GetShopProductVariantBackfillAuditQuery, ShopProductVariantBackfillAuditDto>
{
    private const int MaxDetailLimit = 500;

    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;

    public GetShopProductVariantBackfillAuditQueryHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _shopRepo = shopRepo;
    }

    public async Task<ShopProductVariantBackfillAuditDto> Handle(
        GetShopProductVariantBackfillAuditQuery request,
        CancellationToken cancellationToken)
    {
        var detailLimit = Math.Clamp(request.DetailLimit, 0, MaxDetailLimit);

        var shopProducts = await _shopProductRepo.ListForVariantBackfillAsync(
            request.ShopId,
            request.ProductId,
            cancellationToken);

        var products = await _productRepo.GetByIdsForAdminWithDetailsAsync(
            shopProducts.Select(sp => sp.ProductId).Distinct().ToArray(),
            cancellationToken);

        var shops = await _shopRepo.GetByIdsAsync(
            shopProducts.Select(sp => sp.ShopId).Distinct().ToArray(),
            cancellationToken);

        var productMap = products.ToDictionary(p => p.Id, p => p);
        var shopMap = shops.ToDictionary(s => s.Id, s => s);
        var items = new List<ShopProductVariantBackfillAuditItemDto>();

        var productsWithVariants = 0;
        var existingOfferings = 0;
        var missingOfferings = 0;

        foreach (var shopProduct in shopProducts)
        {
            if (!productMap.TryGetValue(shopProduct.ProductId, out var product) || product.IsDeleted)
                continue;

            var variantIds = product.Variants.Select(v => v.Id).ToHashSet();
            if (variantIds.Count == 0)
                continue;

            productsWithVariants++;

            var existingOfferingCount = shopProduct.VariantOfferings
                .Where(v => !v.IsDeleted && variantIds.Contains(v.ProductVariantId))
                .Select(v => v.ProductVariantId)
                .Distinct()
                .Count();

            var missingOfferingCount = variantIds.Count - existingOfferingCount;
            existingOfferings += existingOfferingCount;
            missingOfferings += missingOfferingCount;

            if (missingOfferingCount <= 0 || items.Count >= detailLimit)
                continue;

            items.Add(new ShopProductVariantBackfillAuditItemDto(
                shopProduct.ShopId,
                shopMap.TryGetValue(shopProduct.ShopId, out var shop) ? shop.Name : string.Empty,
                product.Id,
                product.Title,
                shopProduct.Id,
                variantIds.Count,
                existingOfferingCount,
                missingOfferingCount));
        }

        return new ShopProductVariantBackfillAuditDto(
            shopProducts.Count,
            productsWithVariants,
            existingOfferings,
            missingOfferings,
            items);
    }
}
