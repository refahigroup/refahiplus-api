using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Store.Application.Contracts.Commands.ShopProducts;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Repositories;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariantBackfill;

public class BackfillShopProductVariantsCommandHandler
    : IRequestHandler<BackfillShopProductVariantsCommand, ShopProductVariantBackfillResultDto>
{
    private const int MaxDetailLimit = 500;
    private const int MaxWarnings = 200;

    private readonly IShopProductRepository _shopProductRepo;
    private readonly IProductRepository _productRepo;
    private readonly IShopRepository _shopRepo;
    private readonly ILogger<BackfillShopProductVariantsCommandHandler> _logger;

    public BackfillShopProductVariantsCommandHandler(
        IShopProductRepository shopProductRepo,
        IProductRepository productRepo,
        IShopRepository shopRepo,
        ILogger<BackfillShopProductVariantsCommandHandler> logger)
    {
        _shopProductRepo = shopProductRepo;
        _productRepo = productRepo;
        _shopRepo = shopRepo;
        _logger = logger;
    }

    public async Task<ShopProductVariantBackfillResultDto> Handle(
        BackfillShopProductVariantsCommand request,
        CancellationToken cancellationToken)
    {
        var detailLimit = Math.Clamp(request.DetailLimit, 0, MaxDetailLimit);

        var shopProducts = await _shopProductRepo.ListForVariantBackfillAsync(
            request.ShopId,
            request.ProductId,
            cancellationToken);

        var products = await _productRepo.GetByIdsForAdminWithDetailsAsync(
            shopProducts.Select(sp => sp.ProductId).Distinct().ToArray(),
            cancellationToken);

        var shops = await _shopRepo.GetByIdsAsync(
            shopProducts.Select(sp => sp.ShopId).Distinct().ToArray(),
            cancellationToken);

        var productMap = products.ToDictionary(p => p.Id, p => p);
        var shopMap = shops.ToDictionary(s => s.Id, s => s);
        var createdItems = new List<ShopProductVariantBackfillCreatedItemDto>();
        var warnings = new List<string>();

        var productsWithVariants = 0;
        var createdOfferings = 0;
        var skippedExistingOfferings = 0;
        var skippedInvalidVariants = 0;
        var createdItemsCapped = false;

        foreach (var shopProduct in shopProducts)
        {
            if (!productMap.TryGetValue(shopProduct.ProductId, out var product))
            {
                AddWarning(warnings, $"محصول {shopProduct.ProductId} برای محصول فروشگاه {shopProduct.Id} یافت نشد و رد شد.");
                continue;
            }

            if (product.IsDeleted)
            {
                AddWarning(warnings, $"محصول حذف‌شده {product.Id} برای محصول فروشگاه {shopProduct.Id} رد شد.");
                continue;
            }

            if (product.Variants.Count == 0)
                continue;

            productsWithVariants++;

            var changed = false;
            var existingVariantIds = shopProduct.VariantOfferings
                .Where(v => !v.IsDeleted)
                .Select(v => v.ProductVariantId)
                .ToHashSet();

            foreach (var variant in product.Variants)
            {
                if (existingVariantIds.Contains(variant.Id))
                {
                    skippedExistingOfferings++;
                    continue;
                }

                if (!IsValidVariantPrice(variant, out var warning))
                {
                    skippedInvalidVariants++;
                    AddWarning(warnings, $"{warning} ShopProductId={shopProduct.Id}, ProductVariantId={variant.Id}.");
                    continue;
                }

                createdOfferings++;

                if (createdItems.Count < detailLimit)
                {
                    createdItems.Add(new ShopProductVariantBackfillCreatedItemDto(
                        shopProduct.ShopId,
                        shopMap.TryGetValue(shopProduct.ShopId, out var shop) ? shop.Name : string.Empty,
                        product.Id,
                        product.Title,
                        shopProduct.Id,
                        variant.Id,
                        BuildVariantName(product, variant),
                        variant.PriceMinor,
                        variant.DiscountedPriceMinor));
                }
                else
                {
                    createdItemsCapped = true;
                }

                if (request.DryRun)
                    continue;

                shopProduct.AddVariantOffering(
                    variant.Id,
                    variant.PriceMinor,
                    variant.DiscountedPriceMinor,
                    isActive: true);

                changed = true;
            }

            if (changed)
                await _shopProductRepo.UpdateAsync(shopProduct, cancellationToken);
        }

        if (createdItemsCapped)
            AddWarning(warnings, $"فهرست ردیف‌های ایجادشده در پاسخ به {detailLimit} مورد محدود شد.");

        if (!request.DryRun)
        {
            _logger.LogInformation(
                "ShopProductVariant backfill completed. ShopProductsChecked={ShopProductsChecked}, ProductsWithVariants={ProductsWithVariants}, CreatedOfferings={CreatedOfferings}, SkippedExistingOfferings={SkippedExistingOfferings}, SkippedInvalidVariants={SkippedInvalidVariants}.",
                shopProducts.Count,
                productsWithVariants,
                createdOfferings,
                skippedExistingOfferings,
                skippedInvalidVariants);
        }

        return new ShopProductVariantBackfillResultDto(
            request.DryRun,
            shopProducts.Count,
            productsWithVariants,
            createdOfferings,
            skippedExistingOfferings,
            skippedInvalidVariants,
            createdItems,
            warnings);
    }

    private static bool IsValidVariantPrice(ProductVariant variant, out string warning)
    {
        if (variant.PriceMinor <= 0)
        {
            warning = "قیمت تنوع محصول باید بیشتر از صفر باشد.";
            return false;
        }

        if (variant.DiscountedPriceMinor is <= 0)
        {
            warning = "قیمت تخفیف‌خورده تنوع محصول باید بیشتر از صفر باشد.";
            return false;
        }

        if (variant.DiscountedPriceMinor >= variant.PriceMinor)
        {
            warning = "قیمت تخفیف‌خورده تنوع محصول باید کمتر از قیمت اصلی باشد.";
            return false;
        }

        warning = string.Empty;
        return true;
    }

    private static string BuildVariantName(Product product, ProductVariant variant)
    {
        var parts = variant.Combinations
            .Select(c =>
            {
                var attribute = product.VariantAttributes.FirstOrDefault(a => a.Id == c.VariantAttributeId);
                var value = attribute?.Values.FirstOrDefault(v => v.Id == c.VariantAttributeValueId);

                return attribute is null || value is null
                    ? null
                    : $"{attribute.Name}: {value.Value}";
            })
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length > 0)
            return string.Join(" / ", parts);

        return variant.SKU ?? "تنوع محصول";
    }

    private static void AddWarning(List<string> warnings, string warning)
    {
        if (warnings.Count < MaxWarnings)
        {
            warnings.Add(warning);
            return;
        }

        if (warnings.Count == MaxWarnings)
            warnings.Add("فهرست هشدارها محدود شد.");
    }
}
