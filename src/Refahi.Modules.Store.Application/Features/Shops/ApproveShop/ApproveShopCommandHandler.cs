using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.Shops.ApproveShop;

public class ApproveShopCommandHandler : IRequestHandler<ApproveShopCommand, ApproveShopResponse>
{
    private readonly IShopRepository _shopRepository;

    public ApproveShopCommandHandler(IShopRepository shopRepository)
        => _shopRepository = shopRepository;

    public async Task<ApproveShopResponse> Handle(
        ApproveShopCommand request, CancellationToken cancellationToken)
    {
        var shop = await _shopRepository.GetByIdAsync(request.ShopId, cancellationToken)
            ?? throw new StoreDomainException("فروشگاه یافت نشد", "SHOP_NOT_FOUND");

        shop.Approve();

        await _shopRepository.UpdateAsync(shop, cancellationToken);

        return new ApproveShopResponse(shop.Id, shop.Status.ToString());
    }
}
