using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.DisableShopProduct;

public class DisableShopProductCommandHandler : IRequestHandler<DisableShopProductCommand>
{
    private readonly IShopProductRepository _shopProductRepo;

    public DisableShopProductCommandHandler(IShopProductRepository shopProductRepo)
        => _shopProductRepo = shopProductRepo;

    public async Task Handle(DisableShopProductCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");

        if (shopProduct.IsDeleted)
            throw new StoreDomainException("محصول در این فروشگاه حذف شده است", "SHOP_PRODUCT_DELETED");

        shopProduct.Disable();
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
    }
}
