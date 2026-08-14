using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Features.Catalog;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetModuleShops;

public sealed class GetModuleShopsQueryHandler(
    IStoreModuleRepository modules,
    IPublicCatalogRepository catalog,
    IShopRepository shops,
    IMediator mediator,
    IPathService pathService) : IRequestHandler<GetModuleShopsQuery, ShopsPagedResponse>
{
    public async Task<ShopsPagedResponse> Handle(GetModuleShopsQuery request, CancellationToken ct)
    {
        var empty = new ShopsPagedResponse([], request.PageNumber, request.PageSize, 0, 0);
        var module = await modules.GetByIdAsync(request.ModuleId, ct);
        if (module is null || !module.IsActive || !module.CategoryId.HasValue)
            return empty;
        var categoryIds = await mediator.Send(new GetCategorySubtreeIdsQuery(module.CategoryId.Value), ct);
        var atUtc = DateTimeOffset.UtcNow;
        var coordinates = await catalog.GetEligibilityCoordinatesAsync(
            categoryIds, null, null, null, null, atUtc, ct);
        var allowed = await PublicCatalogEligibility.ResolveAsync(coordinates, mediator, atUtc, ct);
        if (allowed.Count == 0)
            return empty;

        var candidates = new List<PublicCatalogOfferCandidate>();
        foreach (var categoryId in categoryIds)
            candidates.AddRange(await catalog.GetEffectiveCandidatesAsync(
                categoryId, null, null, null, null, null, null, atUtc, ct));
        var allowedSet = allowed.Select(x => (x.SupplierId, x.CategoryId)).ToHashSet();
        var shopIds = candidates.Where(x => allowedSet.Contains((x.SupplierId, x.CategoryId)))
            .Select(x => x.ShopId).Distinct().ToArray();
        if (shopIds.Length == 0)
            return empty;
        var (items, total) = await shops.GetPagedByIdsAsync(
            shopIds, request.PageNumber, request.PageSize, ct);
        return new(items.Select(Map), request.PageNumber, request.PageSize, total,
            (int)Math.Ceiling(total / (double)request.PageSize));
    }

    private ShopSummaryDto Map(Shop shop) => new(
        shop.Id, shop.Name, shop.Slug,
        shop.LogoUrl is null ? null : pathService.MakeAbsoluteMediaUrl(shop.LogoUrl),
        shop.ShopType.ToString(), shop.Status.ToString(), shop.ProvinceId, shop.CityId,
        shop.IsPopular, shop.SupplierId);
}
