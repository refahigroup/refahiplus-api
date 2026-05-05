using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Services.Path;

namespace Refahi.Modules.Store.Application.Features.Shops.AdminGetShop;

public class AdminGetShopQueryHandler : IRequestHandler<AdminGetShopQuery, ShopDto?>
{
    private readonly IShopRepository _shopRepository;
    private readonly IPathService _pathService;

    public AdminGetShopQueryHandler(IShopRepository shopRepository, IPathService pathService)
    {
        _shopRepository = shopRepository;
        _pathService = pathService;
    }

    public async Task<ShopDto?> Handle(AdminGetShopQuery request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepository.GetByIdAsync(request.ShopId, cancellationToken);
        return shop is null ? null : MapToDto(shop);
    }

    private ShopDto MapToDto(Shop s) => new(
        s.Id,
        s.Name,
        s.Slug,
        s.LogoUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(s.LogoUrl),
        s.CoverImageUrl is null ? null : _pathService.MakeAbsoluteMediaUrl(s.CoverImageUrl),
        s.ShopType.ToString(),
        s.Status.ToString(),
        s.SupplierId,
        s.ProvinceId,
        s.CityId,
        s.Address,
        s.Latitude,
        s.Longitude,
        s.ManagerName,
        s.ManagerPhone,
        s.RepresentativeName,
        s.RepresentativePhone,
        s.ContactPhone,
        s.Description,
        s.IsPopular,
        s.CreatedAt);
}
