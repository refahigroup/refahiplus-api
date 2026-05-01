using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.AdminGetDailyDeals;

public class AdminGetDailyDealsQueryHandler : IRequestHandler<AdminGetDailyDealsQuery, List<AdminDailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IShopRepository _shopRepo;

    public AdminGetDailyDealsQueryHandler(
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

    public async Task<List<AdminDailyDealDto>> Handle(AdminGetDailyDealsQuery request, CancellationToken cancellationToken)
    {
        var deals = await _dealRepo.GetAllAsync(request.ModuleId, cancellationToken);

        var result = new List<AdminDailyDealDto>(deals.Count);

        foreach (var deal in deals)
        {
            var product = await _productRepo.GetByIdAsync(deal.ProductId, cancellationToken);
            if (product is null) continue;

            var (shopProducts, _) = await _shopProductRepo.GetByProductAsync(product.Id, isActive: true, page: 1, pageSize: 1, cancellationToken);
            var firstShopProduct = shopProducts.FirstOrDefault();
            var originalPrice = firstShopProduct?.Price ?? 0;
            var shopId = firstShopProduct?.ShopId;
            var shop = shopId.HasValue ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken) : null;

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;
            var discountedPrice = originalPrice * (100 - deal.DiscountPercent) / 100;

            result.Add(new AdminDailyDealDto(
                deal.Id,
                deal.ModuleId,
                deal.ProductId,
                product.Title,
                mainImage,
                originalPrice,
                deal.DiscountPercent,
                discountedPrice,
                deal.StartTime,
                deal.EndTime,
                deal.IsActive,
                shop?.Name ?? string.Empty));
        }

        return result;
    }
}
