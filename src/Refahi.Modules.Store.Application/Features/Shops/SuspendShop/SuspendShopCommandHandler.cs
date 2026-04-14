using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Shops.SuspendShop;

public class SuspendShopCommandHandler : IRequestHandler<SuspendShopCommand, SuspendShopResponse>
{
    private readonly IShopRepository _shopRepository;

    public SuspendShopCommandHandler(IShopRepository shopRepository)
        => _shopRepository = shopRepository;

    public async Task<SuspendShopResponse> Handle(
        SuspendShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepository.GetByIdAsync(request.ShopId, cancellationToken)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");

        shop.Suspend();

        await _shopRepository.UpdateAsync(shop, cancellationToken);

        return new SuspendShopResponse(shop.Id, shop.Status.ToString());
    }
}
