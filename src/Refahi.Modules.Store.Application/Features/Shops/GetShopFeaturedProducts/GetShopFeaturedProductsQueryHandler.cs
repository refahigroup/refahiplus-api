using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShopFeaturedProducts;

public class GetShopFeaturedProductsQueryHandler : IRequestHandler<GetShopFeaturedProductsQuery, List<ShopFeaturedProductDto>>
{
    private readonly IShopRepository _shopRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IPathService _pathService;

    public GetShopFeaturedProductsQueryHandler(
        IShopRepository shopRepo,
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IPathService pathService)
    {
        _shopRepo = shopRepo;
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _pathService = pathService;
    }

    public async Task<List<ShopFeaturedProductDto>> Handle(GetShopFeaturedProductsQuery request, CancellationToken ct)
    {
        var shop = await _shopRepo.GetBySlugAsync(request.ShopSlug, ct);
        if (shop is null) return new();

        var limit = Math.Clamp(request.Limit, 1, 50);

        var (shopProducts, _) = await _shopProductRepo.GetByShopAsync(shop.Id, isActive: true, page: 1, pageSize: limit, ct);
        if (shopProducts.Count == 0) return new();

        var productIds = shopProducts.Select(sp => sp.ProductId).ToList();
        var products = await _productRepo.GetByIdsAsync(productIds, ct);
        var productMap = products
            .Where(p => !p.IsDeleted && p.IsAvailable)
            .ToDictionary(p => p.Id);

        var result = new List<ShopFeaturedProductDto>(shopProducts.Count);
        foreach (var sp in shopProducts)
        {
            if (!productMap.TryGetValue(sp.ProductId, out var product)) continue;

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null ? null : _pathService.MakeAbsoluteMediaUrl(mainImage);

            var hasDiscount = sp.DiscountedPrice > 0 && sp.DiscountedPrice < sp.Price;

            result.Add(new ShopFeaturedProductDto(
                product.Id,
                product.Title,
                product.Slug,
                mainImageUrl,
                sp.Price,
                hasDiscount ? sp.DiscountedPrice : null));
        }

        return result;
    }
}
