using Refahi.Modules.Store.Domain.Enums;

namespace Refahi.Modules.Store.Application.Services;

internal static class StoreSalesModelRules
{
    public const string UnsupportedSessionVariantCode = "SESSION_VARIANT_CAPACITY_UNSUPPORTED";
    public const string UnsupportedSessionVariantMessage =
        "خرید تنوع ظرفیت‌دار برای محصولات سانسی هنوز پشتیبانی نمی‌شود";

    public static bool IsUnsupportedSessionVariant(SalesModel salesModel, Guid? variantId)
        => false;
}
