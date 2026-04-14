using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Shops.UpdateShop;

public class UpdateShopCommandHandler : IRequestHandler<UpdateShopCommand, UpdateShopResponse>
{
    private readonly IShopRepository _shopRepository;

    public UpdateShopCommandHandler(IShopRepository shopRepository)
        => _shopRepository = shopRepository;

    public async Task<UpdateShopResponse> Handle(
        UpdateShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");

        shop.UpdateInfo(
            request.Name,
            request.Description,
            request.City,
            request.Address,
            request.ContactPhone,
            request.LogoUrl,
            request.CoverImageUrl);

        await _shopRepository.UpdateAsync(shop, cancellationToken);

        return new UpdateShopResponse(shop.Id, shop.Name);
    }
}
