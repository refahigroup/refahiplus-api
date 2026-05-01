using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.GetDailyDeals;

public class GetDailyDealsQueryHandler : IRequestHandler<GetDailyDealsQuery, List<DailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IShopRepository _shopRepo;

    public GetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IShopProductRepository shopProductRepo,
        IShopRepository shopRepo)
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
        _shopProductRepo = shopProductRepo;
        _shopRepo = shopRepo;
    }

    public async Task<List<DailyDealDto>> Handle(GetDailyDealsQuery request, CancellationToken cancellationToken)
    {
        var activeDeals = await _dealRepo.GetCurrentlyActiveAsync(cancellationToken);

        IEnumerable<Domain.Entities.DailyDeal> deals = activeDeals;

        if (request.ModuleId.HasValue)
            deals = deals.Where(d => d.ModuleId == request.ModuleId.Value);

        var result = new List<DailyDealDto>();

        foreach (var deal in deals)
        {
            var product = await _productRepo.GetByIdAsync(deal.ProductId, cancellationToken);
            if (product is null || product.IsDeleted)
                continue;

            var (shopProducts, _) = await _shopProductRepo.GetByProductAsync(product.Id, isActive: true, page: 1, pageSize: 1, cancellationToken);
            var firstShopProduct = shopProducts.FirstOrDefault();
            var originalPrice = firstShopProduct?.Price ?? 0;
            var shopId = firstShopProduct?.ShopId;
            var shop = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;

            var discountedPrice = originalPrice * (100 - deal.DiscountPercent) / 100;

            result.Add(new DailyDealDto(
                deal.Id,
                deal.ProductId,
                product.Title,
                mainImage,
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
