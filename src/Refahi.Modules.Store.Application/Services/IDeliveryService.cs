namespace Refahi.Modules.Store.Application.Services;

/// <summary>
/// محاسبه‌ی هزینه ارسال یک سفارش.
/// در فاز ۱ پیاده‌سازی Hardcoded است؛ در آینده می‌تواند بر اساس ShopId, City, Distance, Method, Weight و ... محاسبه شود.
/// </summary>
public interface IDeliveryService
{
    /// <summary>
    /// محاسبه‌ی هزینه‌ی ارسال (به ریال) برای یک سبد بر اساس روش‌های ارسال آیتم‌ها.
    /// </summary>
    /// <param name="items">آیتم‌های سفارش با روش ارسال انتخاب‌شده</param>
    /// <param name="shippingAddressId">شناسه آدرس ارسال (اختیاری برای فاز فعلی)</param>
    /// <param name="shopId">شناسه فروشگاه (اختیاری برای فاز فعلی)</param>
    /// <returns>هزینه ارسال به ریال (Minor unit)</returns>
    long CalcPrice(IReadOnlyList<DeliveryItemInput> items, Guid? shippingAddressId = null, Guid? shopId = null);
}

public sealed record DeliveryItemInput(short DeliveryMethod, int Quantity);
