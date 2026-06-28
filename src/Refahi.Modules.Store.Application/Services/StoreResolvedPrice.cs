namespace Refahi.Modules.Store.Application.Services;

public sealed record StoreResolvedPrice(
    long UnitPriceMinor,
    long OriginalPriceMinor,
    long? DiscountedPriceMinor,
    Guid ShopProductId,
    Guid? ShopProductVariantId,
    Guid? VariantId,
    StorePriceSource Source);

public enum StorePriceSource
{
    ShopProduct = 0,
    ShopProductVariant = 1
}
