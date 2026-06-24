using MediatR;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

public class UpsertShopProductVariantCommandHandler
    : IRequestHandler<UpsertShopProductVariantCommand, ShopProductVariantDto>
{
    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;

    public UpsertShopProductVariantCommandHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
    }

    public async Task<ShopProductVariantDto> Handle(
        UpsertShopProductVariantCommand request,
        CancellationToken cancellationToken)
    {
        var shopProduct = await _shopProductRepo.GetWithVariantOfferingsAsync(
            request.ShopId,
            request.ProductId,
            cancellationToken)
            ?? throw new StoreDomainException("محصول در این فروشگاه یافت نشد", "SHOP_PRODUCT_NOT_FOUND");

        var product = await _productRepo.GetByIdAsync(request.ProductId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        ShopProductVariantValidation.EnsureVariantCanBeOffered(product, request.ProductVariantId);

        var existing = shopProduct.VariantOfferings
            .FirstOrDefault(v => v.ProductVariantId == request.ProductVariantId && !v.IsDeleted);

        var offering = existing is null
            ? shopProduct.AddVariantOffering(
                request.ProductVariantId,
                request.PriceMinor,
                request.DiscountedPriceMinor,
                request.IsActive)
            : shopProduct.UpdateVariantOffering(
                request.ProductVariantId,
                request.PriceMinor,
                request.DiscountedPriceMinor,
                request.IsActive);

        await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);

        return ShopProductVariantMapper.ToDto(offering, product);
    }
}
