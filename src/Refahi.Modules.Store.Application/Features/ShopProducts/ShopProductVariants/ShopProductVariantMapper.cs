using Refahi.Modules.Store.Application.Contracts.Dtos.ShopProducts;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

internal static class ShopProductVariantMapper
{
    public static ShopProductVariantDto ToDto(ShopProductVariant offering, Product? product)
    {
        var productVariant = product?.Variants.FirstOrDefault(v => v.Id == offering.ProductVariantId);

        return new ShopProductVariantDto(
            offering.Id,
            offering.ShopProductId,
            offering.ProductVariantId,
            BuildVariantName(productVariant, product),
            productVariant?.PriceMinor ?? 0,
            productVariant?.DiscountedPriceMinor,
            productVariant?.StockCount ?? 0,
            productVariant?.Capacity,
            offering.PriceMinor,
            offering.DiscountedPriceMinor,
            offering.IsActive,
            offering.IsDeleted);
    }

    private static string BuildVariantName(ProductVariant? variant, Product? product)
    {
        if (variant is null || product is null)
            return string.Empty;

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
}
