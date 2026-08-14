using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShopFeaturedProducts;

public sealed class GetShopFeaturedProductsQueryHandler(
    IShopRepository shops,
    IPublicCatalogRepository catalog,
    IMediator mediator,
    IPathService pathService) : IRequestHandler<GetShopFeaturedProductsQuery, List<ShopFeaturedProductDto>>
{
    public async Task<List<ShopFeaturedProductDto>> Handle(
        GetShopFeaturedProductsQuery request, CancellationToken ct)
    {
        var shop = await shops.GetBySlugAsync(request.ShopSlug, ct);
        if (shop is null)
            return [];
        var atUtc = DateTimeOffset.UtcNow;
        var candidates = await catalog.GetEffectiveCandidatesAsync(
            null, null, shop.Id, null, null, null, null, atUtc, ct);
        var eligible = await PublicCatalogEligibility.FilterAsync(candidates, mediator, atUtc, ct);
        return eligible.GroupBy(x => x.ProductId)
            .OrderByDescending(x => x.First().ProductCreatedAt)
            .ThenBy(x => x.Key)
            .Take(Math.Clamp(request.Limit, 1, 50))
            .Select(group =>
            {
                var product = group.First();
                var offer = group.OrderBy(x => x.FinalPriceMinor).ThenBy(x => x.OfferId).First();
                return new ShopFeaturedProductDto(product.ProductId, product.ProductTitle,
                    product.ProductSlug,
                    product.MainImageUrl is null ? null : pathService.MakeAbsoluteMediaUrl(product.MainImageUrl),
                    offer.OriginalPriceMinor,
                    offer.FinalPriceMinor < offer.OriginalPriceMinor ? offer.FinalPriceMinor : null);
            }).ToList();
    }
}
