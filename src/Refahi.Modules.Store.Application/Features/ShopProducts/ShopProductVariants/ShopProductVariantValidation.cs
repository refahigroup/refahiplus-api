using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;

namespace Refahi.Modules.Store.Application.Features.ShopProducts.ShopProductVariants;

internal static class ShopProductVariantValidation
{
    public static ProductVariant EnsureVariantCanBeOffered(Product product, Guid productVariantId)
    {
        var productVariant = product.Variants.FirstOrDefault(v => v.Id == productVariantId)
            ?? throw new StoreDomainException("تنوع محصول به محصول انتخاب‌شده تعلق ندارد", "PRODUCT_VARIANT_PRODUCT_MISMATCH");

        if (!productVariant.IsAvailable)
            throw new StoreDomainException("تنوع محصول فعال نیست", "PRODUCT_VARIANT_NOT_AVAILABLE");

        return productVariant;
    }
}
