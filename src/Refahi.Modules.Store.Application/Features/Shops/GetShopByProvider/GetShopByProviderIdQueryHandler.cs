using MediatR;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Shops.GetShopByProvider;

public class GetShopByProviderIdQueryHandler : IRequestHandler<GetShopByProviderIdQuery, ShopDto?>
{
    private readonly IShopRepository _shopRepository;

    public GetShopByProviderIdQueryHandler(IShopRepository shopRepository)
        => _shopRepository = shopRepository;

    public async Task<ShopDto?> Handle(
        GetShopByProviderIdQuery request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepository.GetByProviderIdAsync(request.ProviderId, cancellationToken);
        return shop is null ? null : MapToDto(shop);
    }

    private static ShopDto MapToDto(Shop s) => new(
        s.Id,
        s.Name,
        s.Slug,
        s.LogoUrl,
        s.CoverImageUrl,
        s.ShopType.ToString(),
        s.Status.ToString(),
        s.ProviderId,
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
