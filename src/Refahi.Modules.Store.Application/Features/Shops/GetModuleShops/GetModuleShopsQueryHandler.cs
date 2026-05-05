using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetModuleShops;

public class GetModuleShopsQueryHandler : IRequestHandler<GetModuleShopsQuery, ShopsPagedResponse>
{
    private readonly IStoreModuleCatalogService _catalog;
    private readonly IShopProductRepository _shopProductRepository;
    private readonly IShopRepository _shopRepository;
    private readonly IPathService _pathService;

    public GetModuleShopsQueryHandler(
        IStoreModuleCatalogService catalog,
        IShopProductRepository shopProductRepository,
        IShopRepository shopRepository,
        IPathService pathService)
    {
        _catalog = catalog;
        _shopProductRepository = shopProductRepository;
        _shopRepository = shopRepository;
        _pathService = pathService;
    }

    public async Task<ShopsPagedResponse> Handle(GetModuleShopsQuery request, CancellationToken ct)
    {
        var empty = new ShopsPagedResponse([], request.PageNumber, request.PageSize, 0, 0);

        var apIds = await _catalog.GetDisplayableAgreementProductIdsAsync(request.ModuleId, ct);
        if (apIds.Count == 0)
            return empty;

        var shopIds = await _shopProductRepository
            .GetDisplayableShopIdsByAgreementProductIdsAsync(apIds, ct);

        if (shopIds.Count == 0)
            return empty;

        var (items, total) = await _shopRepository.GetPagedByIdsAsync(
            shopIds, request.PageNumber, request.PageSize, ct);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return new ShopsPagedResponse(
            items.Select(MapToDto), request.PageNumber, request.PageSize, total, totalPages);
    }

    private ShopSummaryDto MapToDto(Shop s) => new(
        s.Id, s.Name, s.Slug,
        s.LogoUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(s.LogoUrl),
        s.ShopType.ToString(), s.Status.ToString(),
        s.ProvinceId, s.CityId, s.IsPopular);
}
