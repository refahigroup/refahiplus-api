using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.AdminGetDailyDeals;

public class AdminGetDailyDealsQueryHandler
    : IRequestHandler<AdminGetDailyDealsQuery, List<AdminDailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IOfferRepository _offerRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IPathService _pathService;

    public AdminGetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IOfferRepository offerRepo,
        IShopRepository shopRepo,
        IPathService pathService
    )
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
        _offerRepo = offerRepo;
        _shopRepo = shopRepo;
        _pathService = pathService;
    }

    public async Task<List<AdminDailyDealDto>> Handle(
        AdminGetDailyDealsQuery request,
        CancellationToken cancellationToken
    )
    {
        var deals = await _dealRepo.GetAllAsync(request.ModuleId, ct: cancellationToken);

        var result = new List<AdminDailyDealDto>(deals.Count);

        foreach (var deal in deals)
        {
            var product = await _productRepo.GetByIdAsync(deal.ProductId, cancellationToken);
            if (product is null)
                continue;

            var offerCandidates = await _offerRepo.GetEligibilityCandidatesAsync(
                product.Id,
                deal.ShopId,
                DateTimeOffset.UtcNow,
                cancellationToken
            );
            var offers = new List<Domain.Aggregates.Offer>();
            foreach (var candidate in offerCandidates)
            {
                var offer = await _offerRepo.GetByIdAsync(
                    candidate.OfferId,
                    includeDeleted: false,
                    cancellationToken
                );
                if (offer is not null)
                    offers.Add(offer);
            }
            var firstOffer = offers
                .OrderBy(x => x.FinalPriceMinor)
                .ThenBy(x => x.Id)
                .FirstOrDefault();
            var originalPrice = firstOffer?.OriginalPriceMinor ?? 0;
            var shopId = firstOffer?.ShopId;
            var shop = shopId.HasValue
                ? await _shopRepo.GetByIdAsync(shopId.Value, cancellationToken)
                : null;

            var mainImage =
                product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                ?? product.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null
                ? null
                : _pathService.MakeAbsoluteMediaUrl(mainImage);
            var discountedPrice = firstOffer?.FinalPriceMinor ?? 0;

            result.Add(
                new AdminDailyDealDto(
                    deal.Id,
                    deal.ModuleId,
                    deal.ShopId,
                    deal.ProductId,
                    product.Title,
                    mainImageUrl,
                    originalPrice,
                    deal.DiscountPercent,
                    discountedPrice,
                    deal.StartTime,
                    deal.EndTime,
                    deal.IsActive,
                    shop?.Name ?? string.Empty
                )
            );
        }

        return result;
    }
}
