using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.DailyDeals.GetDailyDeals;

public class GetDailyDealsQueryHandler : IRequestHandler<GetDailyDealsQuery, List<DailyDealDto>>
{
    private readonly IDailyDealRepository _dealRepo;
    private readonly IProductRepository _productRepo;
    private readonly IOfferRepository _offerRepo;
    private readonly IShopRepository _shopRepo;
    private readonly IPathService _pathService;

    public GetDailyDealsQueryHandler(
        IDailyDealRepository dealRepo,
        IProductRepository productRepo,
        IOfferRepository offerRepo,
        IShopRepository shopRepo,
        IMediator mediator,
        IPathService pathService
    )
    {
        _dealRepo = dealRepo;
        _productRepo = productRepo;
        _offerRepo = offerRepo;
        _shopRepo = shopRepo;
        _pathService = pathService;
        _mediator = mediator;
    }

    private readonly IMediator _mediator;

    public async Task<List<DailyDealDto>> Handle(
        GetDailyDealsQuery request,
        CancellationToken cancellationToken
    )
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

            var now = DateTimeOffset.UtcNow;
            var offerCandidates = await _offerRepo.GetEligibilityCandidatesAsync(
                product.Id, deal.ShopId, now, cancellationToken);
            var resolutions = await _mediator.Send(
                new ResolveAgreementCategoryTermsBatchQuery(offerCandidates.Select(x =>
                    new AgreementCategoryTermResolutionRequest(
                        x.SupplierId, x.CategoryId, x.SalesChannel, now)).Distinct().ToArray()),
                cancellationToken);
            var allowed = resolutions.Where(x => x.Term is not null)
                .Select(x => (x.Request.SupplierId, x.Request.CategoryId, x.Request.SalesChannel))
                .ToHashSet();
            var eligibleIds = offerCandidates.Where(x =>
                allowed.Contains((x.SupplierId, x.CategoryId, x.SalesChannel)))
                .Select(x => x.OfferId).ToHashSet();
            var offers = new List<Offer>();
            foreach (var offerId in eligibleIds)
                if (await _offerRepo.GetByIdAsync(offerId, false, cancellationToken) is { } offer
                    && offer.IsEffectiveAt(now))
                    offers.Add(offer);
            var selectedOffer = offers.OrderBy(x => x.FinalPriceMinor).ThenBy(x => x.Id).FirstOrDefault();
            if (selectedOffer is null)
                continue;
            var shop = await _shopRepo.GetByIdAsync(selectedOffer.ShopId, cancellationToken);
            var originalPrice = selectedOffer.OriginalPriceMinor;

            var mainImage =
                product.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl
                ?? product.Images.FirstOrDefault()?.ImageUrl;
            var mainImageUrl = mainImage is null
                ? null
                : _pathService.MakeAbsoluteMediaUrl(mainImage);

            var discountedPrice = selectedOffer.FinalPriceMinor;

            result.Add(
                new DailyDealDto(
                    deal.Id,
                    deal.ProductId,
                    product.Title,
                    mainImageUrl,
                    originalPrice,
                    deal.DiscountPercent,
                    discountedPrice,
                    deal.StartTime,
                    deal.EndTime,
                    shop?.Name ?? string.Empty
                )
            );
        }

        return result;
    }
}
