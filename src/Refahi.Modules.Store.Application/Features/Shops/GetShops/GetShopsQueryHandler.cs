using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShops;

public class GetShopsQueryHandler : IRequestHandler<GetShopsQuery, ShopsPagedResponse>
{
    private readonly IShopRepository _shopRepository;
    private readonly IPathService _pathService;

    public GetShopsQueryHandler(IShopRepository shopRepository, IPathService pathService)
    {
        _shopRepository = shopRepository;
        _pathService = pathService;
    }

    public async Task<ShopsPagedResponse> Handle(
        GetShopsQuery request, CancellationToken cancellationToken)
    {
        ShopType? shopType = request.ShopType.HasValue ? (ShopType)request.ShopType.Value : null;

        ShopStatus? status = request.Status.HasValue
            ? (ShopStatus)request.Status.Value
            : null;

        var (items, total) = await _shopRepository.GetPagedAsync(
            shopType, status, request.PageNumber, request.PageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new ShopsPagedResponse(
            items.Select(MapToSummaryDto),
            request.PageNumber,
            request.PageSize,
            total,
            totalPages);
    }

    private ShopSummaryDto MapToSummaryDto(Shop s) => new(
        s.Id,
        s.Name,
        s.Slug,
        s.LogoUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(s.LogoUrl),
        s.ShopType.ToString(),
        s.Status.ToString(),
        s.ProvinceId,
        s.CityId,
        s.IsPopular);
}
