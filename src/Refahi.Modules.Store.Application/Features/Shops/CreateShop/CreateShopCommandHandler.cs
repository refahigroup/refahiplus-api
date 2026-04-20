using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Shops.CreateShop;

public class CreateShopCommandHandler : IRequestHandler<CreateShopCommand, CreateShopResponse>
{
    private readonly IShopRepository _shopRepository;

    public CreateShopCommandHandler(IShopRepository shopRepository)
        => _shopRepository = shopRepository;

    public async Task<CreateShopResponse> Handle(
        CreateShopCommand request, CancellationToken cancellationToken)
    {
        if (await _shopRepository.SlugExistsAsync(request.Slug.Trim().ToLower(), cancellationToken))
            throw new StoreDomainException("این اسلاگ قبلاً ثبت شده است", "SLUG_ALREADY_EXISTS");

        if (await _shopRepository.ProviderHasShopAsync(request.ProviderId, cancellationToken))
            throw new StoreDomainException("این تامین‌کننده قبلاً فروشگاه ثبت کرده است", "PROVIDER_ALREADY_HAS_SHOP");

        var shopType = (ShopType)request.ShopType;

        var shop = Shop.Create(
            request.Name,
            request.Slug,
            shopType,
            request.ProviderId,
            request.ProvinceId,
            request.CityId,
            request.Address,
            request.Latitude,
            request.Longitude,
            request.ManagerName,
            request.ManagerPhone,
            request.RepresentativeName,
            request.RepresentativePhone,
            request.ContactPhone,
            request.Description);

        await _shopRepository.AddAsync(shop, cancellationToken);

        return new CreateShopResponse(shop.Id, shop.Name, shop.Slug, shop.Status.ToString());
    }
}
