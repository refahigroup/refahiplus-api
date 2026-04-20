using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.AdminGetDailyDeals;

public class AdminGetDailyDealsQueryHandler : IRequestHandler<AdminGetDailyDealsQuery, List<AdminDailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;

    public AdminGetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo)
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
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

            var shop = await _shopRepo.GetByIdAsync(product.ShopId, cancellationToken);
            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;
            var discountedPrice = product.PriceMinor * (100 - deal.DiscountPercent) / 100;

            result.Add(new AdminDailyDealDto(
                deal.Id,
                deal.ModuleId,
                deal.ProductId,
                product.Title,
                mainImage,
                product.PriceMinor,
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
