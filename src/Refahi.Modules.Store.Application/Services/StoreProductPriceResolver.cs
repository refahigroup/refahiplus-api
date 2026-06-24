using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Services;

public sealed class StoreProductPriceResolver : IStoreProductPriceResolver
{
    private readonly IProductRepository _productRepository;
    private readonly IShopProductRepository _shopProductRepository;
    private readonly ILogger<StoreProductPriceResolver> _logger;

    public StoreProductPriceResolver(
        IProductRepository productRepository,
        IShopProductRepository shopProductRepository,
        ILogger<StoreProductPriceResolver> logger)
    {
        _productRepository = productRepository;
        _shopProductRepository = shopProductRepository;
        _logger = logger;
    }

    public async Task<StoreResolvedPrice> ResolveAsync(
        Guid shopId,
        Guid productId,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(productId, cancellationToken)
            ?? throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        return await ResolveAsync(shopId, product, variantId, cancellationToken);
    }

    public async Task<StoreResolvedPrice> ResolveAsync(
        Guid shopId,
        Product product,
        Guid? variantId,
        CancellationToken cancellationToken = default)
    {
        if (product.IsDeleted)
            throw new StoreDomainException("محصول یافت نشد", "PRODUCT_NOT_FOUND");

        var shopProduct = await _shopProductRepository.GetWithVariantOfferingsAsync(
            shopId,
            product.Id,
            cancellationToken);

        if (shopProduct is null || !shopProduct.IsActive)
            throw new StoreDomainException("این محصول در فروشگاه مورد نظر موجود نیست", "PRODUCT_NOT_IN_SHOP");

        if (!variantId.HasValue)
        {
            var discountedPrice = NormalizeShopDiscount(shopProduct.DiscountedPrice);
            ValidatePrice(shopProduct.Price, discountedPrice);

            return new StoreResolvedPrice(
                UnitPriceMinor: discountedPrice ?? shopProduct.Price,
                OriginalPriceMinor: shopProduct.Price,
                DiscountedPriceMinor: discountedPrice,
                ShopProductId: shopProduct.Id,
                ShopProductVariantId: null,
                VariantId: null,
                Source: StorePriceSource.ShopProduct,
                UsedFallback: false);
        }

        var variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value)
            ?? throw new StoreDomainException("تنوع محصول یافت نشد", "VARIANT_NOT_FOUND");

        var offering = shopProduct.VariantOfferings
            .FirstOrDefault(v => v.ProductVariantId == variant.Id && !v.IsDeleted);

        if (offering is not null)
        {
            if (!offering.IsActive)
                throw new StoreDomainException("این تنوع در فروشگاه انتخاب‌شده فعال نیست.", "SHOP_PRODUCT_VARIANT_INACTIVE");

            ValidatePrice(offering.PriceMinor, offering.DiscountedPriceMinor);

            return new StoreResolvedPrice(
                UnitPriceMinor: offering.DiscountedPriceMinor ?? offering.PriceMinor,
                OriginalPriceMinor: offering.PriceMinor,
                DiscountedPriceMinor: offering.DiscountedPriceMinor,
                ShopProductId: shopProduct.Id,
                ShopProductVariantId: offering.Id,
                VariantId: variant.Id,
                Source: StorePriceSource.ShopProductVariant,
                UsedFallback: false);
        }

        ValidatePrice(variant.PriceMinor, variant.DiscountedPriceMinor);

        _logger.LogInformation(
            "Using ProductVariant price fallback for shop {ShopId}, product {ProductId}, variant {VariantId}.",
            shopId,
            product.Id,
            variant.Id);

        return new StoreResolvedPrice(
            UnitPriceMinor: variant.DiscountedPriceMinor ?? variant.PriceMinor,
            OriginalPriceMinor: variant.PriceMinor,
            DiscountedPriceMinor: variant.DiscountedPriceMinor,
            ShopProductId: shopProduct.Id,
            ShopProductVariantId: null,
            VariantId: variant.Id,
            Source: StorePriceSource.ProductVariantFallback,
            UsedFallback: true);
    }

    private static long? NormalizeShopDiscount(long discountedPrice)
        => discountedPrice > 0 ? discountedPrice : null;

    private static void ValidatePrice(long priceMinor, long? discountedPriceMinor)
    {
        if (priceMinor <= 0)
            throw new StoreDomainException("قیمت باید بیشتر از صفر باشد", "INVALID_PRICE");

        if (discountedPriceMinor is <= 0)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید بیشتر از صفر باشد", "INVALID_DISCOUNTED_PRICE");

        if (discountedPriceMinor >= priceMinor)
            throw new StoreDomainException("قیمت تخفیف‌خورده باید کمتر از قیمت اصلی باشد", "INVALID_DISCOUNTED_PRICE");
    }
}

