using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.GetDailyDeals;

public class GetDailyDealsQueryHandler : IRequestHandler<GetDailyDealsQuery, List<DailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;

    public GetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo)
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
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

            var shop = await _shopRepo.GetByIdAsync(product.ShopId, cancellationToken);

            var mainImage = product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                         ?? product.Images.FirstOrDefault()?.ImageUrl;

            var discountedPrice = product.PriceMinor * (100 - deal.DiscountPercent) / 100;

            result.Add(new DailyDealDto(
                deal.Id,
                deal.ProductId,
                product.Title,
                mainImage,
                product.PriceMinor,
                deal.DiscountPercent,
                discountedPrice,
                deal.StartTime,
                deal.EndTime,
                shop?.Name ?? string.Empty));
        }

        return result;
    }
}
