using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.GetDailyDeals;

public class GetDailyDealsQueryHandler : IRequestHandler<GetDailyDealsQuery, List<DailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IPathService _pathService;

    public GetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IShopRepository shopRepo,
        IPathService pathService)
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _shopRepo = shopRepo;
        _pathService = pathService;
    }

    public async Task<List<DailyDealDto>> Handle(GetDailyDealsQuery request, CancellationToken cancellationToken)
    {
        List<DailyDeal> deals;

        if (request.OwnerType == BannerOwnerType.Module)
        {
            if (!int.TryParse(request.OwnerId, out var moduleId))
                return new();
            deals = await _dealRepo.GetCurrentlyActiveByModuleAsync(moduleId, cancellationToken);
        }
        else if (request.OwnerType == BannerOwnerType.Shop)
        {
            if (!Guid.TryParse(request.OwnerId, out var shopId))
                return new();
            deals = await _dealRepo.GetCurrentlyActiveByShopAsync(shopId, cancellationToken);
        }
        else
        {
            return new();
        }

        var result = new List<DailyDealDto>();

        foreach (var deal in deals)
        {
            var product = await _productRepo.GetByIdAsync(deal.ProductId, cancellationToken);
            if (product is null || product.IsDeleted)
                continue;

            ShopProduct? firstShopProduct;
            Shop? shop;

            if (deal.ShopId.HasValue)
            {
                firstShopProduct = await _shopProductRepo.GetAsync(deal.ShopId.Value, product.Id, cancellationToken);
                shop = await _shopRepo.GetByIdAsync(deal.ShopId.Value, cancellationToken);
            }
            else
            {
                var (shopProducts, _) = await _shopProductRepo.GetByProductAsync(product.Id, isActive: true, page: 1, pageSize: 1, cancellationToken);
                firstShopProduct = shopProducts.FirstOrDefault();
                var shopId = firstShopProduct?.ShopId;
                shop = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;
            }

            var originalPrice = firstShopProduct?.Price ?? 0;

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null ? null : _pathService.MakeAbsoluteMediaUrl(mainImage);

            var discountedPrice = originalPrice * (100 - deal.DiscountPercent) / 100;

            result.Add(new DailyDealDto(
                deal.Id,
                deal.ProductId,
                product.Title,
                mainImageUrl,
                originalPrice,
                deal.DiscountPercent,
                discountedPrice,
                deal.StartTime,
                deal.EndTime,
                shop?.Name ?? string.Empty));
        }

        return result;
    }
}
