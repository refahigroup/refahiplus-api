using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.RemoveProductFromShop;

public class RemoveProductFromShopCommandHandler : IRequestHandler<RemoveProductFromShopCommand, Unit>
{
    private readonly IShopProductRepository _shopProductRepo;

    public RemoveProductFromShopCommandHandler(IShopProductRepository shopProductRepo)
        => _shopProductRepo = shopProductRepo;

    public async Task<Unit> Handle(RemoveProductFromShopCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await _shopProductRepo.GetAsync(request.ShopId, request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");

        shopProduct.SoftDelete();
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        return Unit.Value;
    }
}
