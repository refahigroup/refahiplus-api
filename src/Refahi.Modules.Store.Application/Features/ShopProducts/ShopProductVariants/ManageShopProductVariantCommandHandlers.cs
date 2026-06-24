using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

public class EnableShopProductVariantCommandHandler
    : IRequestHandler<EnableShopProductVariantCommand, Unit>
{
    private readonly IShopProductRepository _shopProductRepo;

    public EnableShopProductVariantCommandHandler(IShopProductRepository shopProductRepo)
        => _shopProductRepo = shopProductRepo;

    public async Task<Unit> Handle(EnableShopProductVariantCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await GetShopProductAsync(request.ShopId, request.ProductId, cancellationToken);
        shopProduct.EnableVariantOffering(request.ProductVariantId);
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        return Unit.Value;
    }

    private async Task<ShopProduct> GetShopProductAsync(
        Guid shopId,
        Guid productId,
        CancellationToken cancellationToken)
        => await _shopProductRepo.GetWithVariantOfferingsAsync(shopId, productId, cancellationToken)
           ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");
}

public class DisableShopProductVariantCommandHandler
    : IRequestHandler<DisableShopProductVariantCommand, Unit>
{
    private readonly IShopProductRepository _shopProductRepo;

    public DisableShopProductVariantCommandHandler(IShopProductRepository shopProductRepo)
        => _shopProductRepo = shopProductRepo;

    public async Task<Unit> Handle(DisableShopProductVariantCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await GetShopProductAsync(request.ShopId, request.ProductId, cancellationToken);
        shopProduct.DisableVariantOffering(request.ProductVariantId);
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        return Unit.Value;
    }

    private async Task<ShopProduct> GetShopProductAsync(
        Guid shopId,
        Guid productId,
        CancellationToken cancellationToken)
        => await _shopProductRepo.GetWithVariantOfferingsAsync(shopId, productId, cancellationToken)
           ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");
}

public class RemoveShopProductVariantCommandHandler
    : IRequestHandler<RemoveShopProductVariantCommand, Unit>
{
    private readonly IShopProductRepository _shopProductRepo;

    public RemoveShopProductVariantCommandHandler(IShopProductRepository shopProductRepo)
        => _shopProductRepo = shopProductRepo;

    public async Task<Unit> Handle(RemoveShopProductVariantCommand request, CancellationToken cancellationToken)
    {
        var shopProduct = await GetShopProductAsync(request.ShopId, request.ProductId, cancellationToken);
        shopProduct.RemoveVariantOffering(request.ProductVariantId);
        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        return Unit.Value;
    }

    private async Task<ShopProduct> GetShopProductAsync(
        Guid shopId,
        Guid productId,
        CancellationToken cancellationToken)
        => await _shopProductRepo.GetWithVariantOfferingsAsync(shopId, productId, cancellationToken)
           ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");
}
